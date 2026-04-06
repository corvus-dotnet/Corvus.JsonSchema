// <copyright file="FunctionalEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using Corvus.Numerics;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// A functional (tree-walking) JsonLogic evaluator that compiles rules into
/// delegate trees for zero-dispatch-overhead evaluation. Each operator becomes
/// a direct function call; intermediate values flow through return values
/// (hardware registers) rather than an explicit stack array.
/// </summary>
internal static class FunctionalEvaluator
{
    /// <summary>
    /// The compiled delegate type. Each rule compiles to a single delegate that
    /// evaluates the rule against the provided data and workspace.
    /// </summary>
    /// <param name="data">The current data context.</param>
    /// <param name="workspace">The workspace for intermediate document allocation.</param>
    /// <returns>The evaluation result.</returns>
    internal delegate EvalResult RuleEvaluator(in JsonElement data, JsonWorkspace workspace);

    [ThreadStatic]
    private static ReduceContext? t_reduceContext;

    /// <summary>
    /// Evaluates a pre-compiled rule delegate against data.
    /// </summary>
    internal static JsonElement Execute(RuleEvaluator rule, in JsonElement data, JsonWorkspace workspace, bool cloneResult)
    {
        EvalResult result = rule(data, workspace);
        JsonElement element = result.AsElement(workspace);
        return cloneResult ? element.Clone() : element;
    }

    /// <summary>
    /// Compiles a JsonLogic rule JSON element into a delegate tree.
    /// </summary>
    internal static RuleEvaluator Compile(in JsonElement rule, Dictionary<string, IJsonLogicOperator>? operators = null)
    {
        return CompileExpression(rule, operators);
    }

    private static RuleEvaluator CompileExpression(in JsonElement rule, Dictionary<string, IJsonLogicOperator>? operators)
    {
        if (rule.ValueKind == JsonValueKind.Object && rule.GetPropertyCount() > 0)
        {
            return CompileOperatorCall(rule, operators);
        }

        if (rule.ValueKind == JsonValueKind.Array)
        {
            return CompileArrayLiteral(rule, operators);
        }

        // Literal value — capture as constant
        return CompileLiteral(rule);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static RuleEvaluator CompileLiteral(in JsonElement value)
    {
        // Number literals: extract the double at compile time so downstream
        // comparisons and arithmetic can use the fast IsDouble path.
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double d))
        {
            return (in JsonElement data, JsonWorkspace workspace) => EvalResult.FromDouble(d);
        }

        JsonElement captured = value;
        return (in JsonElement data, JsonWorkspace workspace) => EvalResult.FromElement(captured);
    }

    private static RuleEvaluator CompileArrayLiteral(in JsonElement rule, Dictionary<string, IJsonLogicOperator>? operators)
    {
        int count = rule.GetArrayLength();
        if (count == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.EmptyArray());
        }

        RuleEvaluator[] items = new RuleEvaluator[count];
        int i = 0;
        foreach (JsonElement item in rule.EnumerateArray())
        {
            items[i++] = CompileExpression(item, operators);
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, items.Length);
            JsonElement.Mutable root = doc.RootElement;

            for (int j = 0; j < items.Length; j++)
            {
                root.AddItem(items[j](data, workspace).AsElement(workspace));
            }

            return EvalResult.FromElement((JsonElement)root);
        };
    }

    private static RuleEvaluator CompileOperatorCall(in JsonElement rule, Dictionary<string, IJsonLogicOperator>? operators)
    {
        foreach (JsonProperty<JsonElement> property in rule.EnumerateObject())
        {
            string opName = property.Name;
            JsonElement args = property.Value;

            RuleEvaluator[] operands = CompileArgs(args, operators);

            return opName switch
            {
                "var" => CompileVar(args, operators),
                "+" => CompileAdd(operands),
                "-" => CompileSub(operands),
                "*" => CompileMul(operands),
                "/" => CompileDiv(operands),
                "%" => CompileMod(operands),
                "min" => CompileMinMax(operands, isMin: true),
                "max" => CompileMinMax(operands, isMin: false),
                "cat" => CompileCat(operands),
                "substr" => CompileSubstr(operands),
                "and" => CompileAnd(operands),
                "or" => CompileOr(operands),
                "if" or "?:" => CompileIf(operands),
                "==" => CompileEquals(operands),
                "===" => CompileStrictEquals(operands),
                "!=" => CompileNotEquals(operands),
                "!==" => CompileStrictNotEquals(operands),
                "!" => CompileNot(operands),
                "!!" => CompileTruthy(operands),
                ">" => CompileComparison(operands, CompareOp.GreaterThan),
                ">=" => CompileComparison(operands, CompareOp.GreaterThanOrEqual),
                "<" => CompileComparison(operands, CompareOp.LessThan),
                "<=" => CompileComparison(operands, CompareOp.LessThanOrEqual),
                "in" => CompileIn(operands),
                "merge" => CompileMerge(operands),
                "map" => CompileMap(args, operators),
                "filter" => CompileFilter(args, operators),
                "reduce" => CompileReduce(args, operators),
                "all" => CompileQuantifier(args, operators, QuantifierKind.All),
                "none" => CompileQuantifier(args, operators, QuantifierKind.None),
                "some" => CompileQuantifier(args, operators, QuantifierKind.Some),
                "missing" => CompileMissing(operands),
                "missing_some" => CompileMissingSome(args, operators),
                "log" => CompileLog(operands),
                "asDouble" => CompileAsDouble(operands),
                "asLong" => CompileAsLong(operands),
                "asBigNumber" => CompileAsBigNumber(operands),
                "asBigInteger" => CompileAsBigInteger(operands),
                _ => throw new InvalidOperationException($"Unknown JsonLogic operator: {opName}"),
            };
        }

        // Empty object — treat as literal
        return CompileLiteral(rule);
    }

    private static RuleEvaluator[] CompileArgs(in JsonElement args, Dictionary<string, IJsonLogicOperator>? operators)
    {
        if (args.ValueKind == JsonValueKind.Array)
        {
            int count = args.GetArrayLength();
            RuleEvaluator[] result = new RuleEvaluator[count];
            int i = 0;
            foreach (JsonElement arg in args.EnumerateArray())
            {
                result[i++] = CompileExpression(arg, operators);
            }

            return result;
        }

        return [CompileExpression(args, operators)];
    }

    // ─── VAR ─────────────────────────────────────────────────────
    private static RuleEvaluator CompileVar(in JsonElement args, Dictionary<string, IJsonLogicOperator>? operators)
    {
        JsonElement pathArg;
        RuleEvaluator? defaultExpr = null;

        if (args.ValueKind == JsonValueKind.Array)
        {
            int count = args.GetArrayLength();
            if (count == 0)
            {
                return static (in JsonElement data, JsonWorkspace workspace) => EvalResult.FromElement(data);
            }

            pathArg = args[0];
            if (count >= 2)
            {
                defaultExpr = CompileExpression(args[1], operators);
            }
        }
        else
        {
            pathArg = args;
        }

        // ReduceContext interception: when compiling a reduce body, intercept
        // "current" and "accumulator" var lookups to read directly from the
        // captured ReduceContext, avoiding JSON object construction entirely.
        if (t_reduceContext is ReduceContext reduceCtx
            && pathArg.ValueKind == JsonValueKind.String
            && !pathArg.IsNullOrUndefined()
            && !JsonLogicHelpers.IsEmptyString(pathArg))
        {
            byte[][] segs = PrecomputePathSegments(pathArg);
            if (segs.Length == 1)
            {
                if (segs[0].AsSpan().SequenceEqual("current"u8))
                {
                    ReduceContext captured = reduceCtx;
                    return (in JsonElement data, JsonWorkspace workspace) => captured.Current;
                }

                if (segs[0].AsSpan().SequenceEqual("accumulator"u8))
                {
                    ReduceContext captured = reduceCtx;
                    return (in JsonElement data, JsonWorkspace workspace) => captured.Accumulator;
                }
            }
        }

        // Empty path or null: return entire data
        if (pathArg.IsNullOrUndefined()
            || (pathArg.ValueKind == JsonValueKind.String && JsonLogicHelpers.IsEmptyString(pathArg)))
        {
            return static (in JsonElement data, JsonWorkspace workspace) => EvalResult.FromElement(data);
        }

        // Dynamic path (computed expression)
        if (pathArg.ValueKind == JsonValueKind.Object && pathArg.GetPropertyCount() > 0)
        {
            RuleEvaluator pathExpr = CompileExpression(pathArg, operators);
            RuleEvaluator? def = defaultExpr;
            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                JsonElement path = pathExpr(data, workspace).AsElement(workspace);
                JsonElement resolved = ResolveVar(data, path);
                if (resolved.IsNullOrUndefined() && def is not null)
                {
                    return def(data, workspace);
                }

                return EvalResult.FromElement(resolved);
            };
        }

        // Pre-compute path segments at compile time
        byte[][] segments = PrecomputePathSegments(pathArg);
        RuleEvaluator? defVal = defaultExpr;

        if (segments.Length == 0)
        {
            // Empty path
            return static (in JsonElement data, JsonWorkspace workspace) => EvalResult.FromElement(data);
        }

        if (segments.Length == 1 && defVal is null)
        {
            // Single-segment fast path (most common: {"var":"x"})
            byte[] prop = segments[0];
            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(prop, out JsonElement value))
                {
                    return EvalResult.FromElement(value);
                }

                if (data.ValueKind == JsonValueKind.Array
                    && TryParseIndexUtf8(prop, out int idx)
                    && idx >= 0 && idx < data.GetArrayLength())
                {
                    return EvalResult.FromElement(data[idx]);
                }

                return EvalResult.FromElement(JsonLogicHelpers.NullElement());
            };
        }

        // Multi-segment path or path with default
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement current = data;
            for (int i = 0; i < segments.Length; i++)
            {
                byte[] seg = segments[i];
                if (current.ValueKind == JsonValueKind.Object)
                {
                    if (!current.TryGetProperty(seg, out current))
                    {
                        return defVal is not null
                            ? defVal(data, workspace)
                            : EvalResult.FromElement(JsonLogicHelpers.NullElement());
                    }
                }
                else if (current.ValueKind == JsonValueKind.Array)
                {
                    if (TryParseIndexUtf8(seg, out int idx) && idx >= 0 && idx < current.GetArrayLength())
                    {
                        current = current[idx];
                    }
                    else
                    {
                        return defVal is not null
                            ? defVal(data, workspace)
                            : EvalResult.FromElement(JsonLogicHelpers.NullElement());
                    }
                }
                else
                {
                    return defVal is not null
                        ? defVal(data, workspace)
                        : EvalResult.FromElement(JsonLogicHelpers.NullElement());
                }
            }

            return EvalResult.FromElement(current);
        };
    }

    private static byte[][] PrecomputePathSegments(in JsonElement pathArg)
    {
        if (pathArg.ValueKind == JsonValueKind.Number)
        {
            // Numeric path — get raw UTF-8
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(pathArg);
            return [raw.Span.ToArray()];
        }

        if (pathArg.ValueKind == JsonValueKind.String)
        {
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(pathArg);
            ReadOnlySpan<byte> quoted = raw.Span;
            if (quoted.Length <= 2)
            {
                return [];
            }

            ReadOnlySpan<byte> unquoted = quoted.Slice(1, quoted.Length - 2);

            // Count dots to pre-allocate
            int dotCount = 0;
            for (int i = 0; i < unquoted.Length; i++)
            {
                if (unquoted[i] == (byte)'.')
                {
                    dotCount++;
                }
            }

            byte[][] segments = new byte[dotCount + 1][];
            int segIndex = 0;
            while (unquoted.Length > 0)
            {
                int dot = unquoted.IndexOf((byte)'.');
                if (dot >= 0)
                {
                    segments[segIndex++] = unquoted.Slice(0, dot).ToArray();
                    unquoted = unquoted.Slice(dot + 1);
                }
                else
                {
                    segments[segIndex++] = unquoted.ToArray();
                    break;
                }
            }

            return segments;
        }

        return [];
    }

    // ─── ARITHMETIC ──────────────────────────────────────────────
    private static RuleEvaluator CompileAdd(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.Zero());
        }

        if (operands.Length == 1)
        {
            // Unary +: coerce to number
            RuleEvaluator op = operands[0];
            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                EvalResult r = op(data, workspace);
                if (r.TryGetDouble(out double d))
                {
                    return EvalResult.FromDouble(d);
                }

                return EvalResult.FromElement(JsonLogicHelpers.Zero());
            };
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            double sum = 0;
            for (int i = 0; i < operands.Length; i++)
            {
                EvalResult r = operands[i](data, workspace);
                if (r.TryGetDouble(out double d))
                {
                    sum += d;
                }
                else
                {
                    return AddSlow(operands, i, sum, data, workspace);
                }
            }

            return EvalResult.FromDouble(sum);
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static EvalResult AddSlow(RuleEvaluator[] operands, int startIndex, double partialSum, in JsonElement data, JsonWorkspace workspace)
    {
        BigNumber sum = (BigNumber)partialSum;
        JsonElement elem = operands[startIndex](data, workspace).AsElement(workspace);
        sum += CoerceToBigNumber(elem);

        for (int i = startIndex + 1; i < operands.Length; i++)
        {
            elem = operands[i](data, workspace).AsElement(workspace);
            sum += CoerceToBigNumber(elem);
        }

        return EvalResult.FromElement(BigNumberToElement(sum, workspace));
    }

    private static RuleEvaluator CompileSub(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.Zero());
        }

        if (operands.Length == 1)
        {
            RuleEvaluator op = operands[0];
            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                EvalResult r = op(data, workspace);
                if (r.TryGetDouble(out double d))
                {
                    return EvalResult.FromDouble(-d);
                }

                return EvalResult.FromElement(
                    BigNumberToElement(-CoerceToBigNumber(r.AsElement(workspace)), workspace));
            };
        }

        RuleEvaluator left = operands[0];
        RuleEvaluator right = operands[1];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult lr = left(data, workspace);
            EvalResult rr = right(data, workspace);
            if (lr.TryGetDouble(out double ld) && rr.TryGetDouble(out double rd))
            {
                return EvalResult.FromDouble(ld - rd);
            }

            return EvalResult.FromElement(
                BigNumberToElement(
                    CoerceToBigNumber(lr.AsElement(workspace)) - CoerceToBigNumber(rr.AsElement(workspace)),
                    workspace));
        };
    }

    private static RuleEvaluator CompileMul(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.Zero());
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            double product = 1;
            for (int i = 0; i < operands.Length; i++)
            {
                EvalResult r = operands[i](data, workspace);
                if (r.TryGetDouble(out double d))
                {
                    product *= d;
                }
                else
                {
                    return MulSlow(operands, i, product, data, workspace);
                }
            }

            return EvalResult.FromDouble(product);
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static EvalResult MulSlow(RuleEvaluator[] operands, int startIndex, double partialProduct, in JsonElement data, JsonWorkspace workspace)
    {
        BigNumber product = (BigNumber)partialProduct;
        JsonElement elem = operands[startIndex](data, workspace).AsElement(workspace);
        product *= CoerceToBigNumber(elem);

        for (int i = startIndex + 1; i < operands.Length; i++)
        {
            elem = operands[i](data, workspace).AsElement(workspace);
            product *= CoerceToBigNumber(elem);
        }

        return EvalResult.FromElement(BigNumberToElement(product, workspace));
    }

    private static RuleEvaluator CompileDiv(RuleEvaluator[] operands)
    {
        if (operands.Length < 2)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.NullElement());
        }

        RuleEvaluator left = operands[0];
        RuleEvaluator right = operands[1];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult lr = left(data, workspace);
            EvalResult rr = right(data, workspace);
            if (lr.TryGetDouble(out double ld) && rr.TryGetDouble(out double rd))
            {
                return rd == 0 ? EvalResult.FromElement(JsonLogicHelpers.NullElement()) : EvalResult.FromDouble(ld / rd);
            }

            BigNumber rb = CoerceToBigNumber(rr.AsElement(workspace));
            if (rb == BigNumber.Zero)
            {
                return EvalResult.FromElement(JsonLogicHelpers.NullElement());
            }

            return EvalResult.FromElement(
                BigNumberToElement(CoerceToBigNumber(lr.AsElement(workspace)) / rb, workspace));
        };
    }

    private static RuleEvaluator CompileMod(RuleEvaluator[] operands)
    {
        if (operands.Length < 2)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.NullElement());
        }

        RuleEvaluator left = operands[0];
        RuleEvaluator right = operands[1];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult lr = left(data, workspace);
            EvalResult rr = right(data, workspace);
            if (lr.TryGetDouble(out double ld) && rr.TryGetDouble(out double rd))
            {
                return rd == 0 ? EvalResult.FromElement(JsonLogicHelpers.NullElement()) : EvalResult.FromDouble(ld % rd);
            }

            BigNumber rb = CoerceToBigNumber(rr.AsElement(workspace));
            if (rb == BigNumber.Zero)
            {
                return EvalResult.FromElement(JsonLogicHelpers.NullElement());
            }

            return EvalResult.FromElement(
                BigNumberToElement(CoerceToBigNumber(lr.AsElement(workspace)) % rb, workspace));
        };
    }

    private static RuleEvaluator CompileMinMax(RuleEvaluator[] operands, bool isMin)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.NullElement());
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult best = operands[0](data, workspace);
            if (!best.TryGetDouble(out double bestD))
            {
                return MinMaxSlow(operands, isMin, data, workspace);
            }

            for (int i = 1; i < operands.Length; i++)
            {
                EvalResult r = operands[i](data, workspace);
                if (!r.TryGetDouble(out double d))
                {
                    return MinMaxSlow(operands, isMin, data, workspace);
                }

                if (isMin ? d < bestD : d > bestD)
                {
                    bestD = d;
                }
            }

            return EvalResult.FromDouble(bestD);
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static EvalResult MinMaxSlow(RuleEvaluator[] operands, bool isMin, in JsonElement data, JsonWorkspace workspace)
    {
        JsonElement best = operands[0](data, workspace).AsElement(workspace);
        if (!JsonLogicHelpers.TryCoerceToNumber(best, out best))
        {
            return EvalResult.FromElement(JsonLogicHelpers.NullElement());
        }

        for (int i = 1; i < operands.Length; i++)
        {
            JsonElement elem = operands[i](data, workspace).AsElement(workspace);
            if (!JsonLogicHelpers.TryCoerceToNumber(elem, out elem))
            {
                return EvalResult.FromElement(JsonLogicHelpers.NullElement());
            }

            int cmp = JsonLogicHelpers.CompareNumbers(elem, best);
            if (isMin ? cmp < 0 : cmp > 0)
            {
                best = elem;
            }
        }

        return EvalResult.FromElement(best);
    }

    // ─── COMPARISON ──────────────────────────────────────────────
    private enum CompareOp
    {
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
    }

    private static RuleEvaluator CompileComparison(RuleEvaluator[] operands, CompareOp op)
    {
        if (operands.Length < 2)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
        }

        if (operands.Length == 2)
        {
            RuleEvaluator left = operands[0];
            RuleEvaluator right = operands[1];
            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                EvalResult lr = left(data, workspace);
                EvalResult rr = right(data, workspace);
                return EvalResult.FromElement(
                    JsonLogicHelpers.BooleanElement(CompareCoerced(lr, rr, op, workspace)));
            };
        }

        // Between: {"<":[a, b, c]} means a < b && b < c
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult prev = operands[0](data, workspace);
            for (int i = 1; i < operands.Length; i++)
            {
                EvalResult curr = operands[i](data, workspace);
                if (!CompareCoerced(prev, curr, op, workspace))
                {
                    return EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
                }

                prev = curr;
            }

            return EvalResult.FromElement(JsonLogicHelpers.BooleanElement(true));
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CompareCoerced(in EvalResult left, in EvalResult right, CompareOp op, JsonWorkspace workspace)
    {
        // Use TryGetDouble which handles both native doubles AND number/bool/null
        // elements, widening the fast path to avoid CompareCoercedElement overhead.
        if (left.TryGetDouble(out double ld) && right.TryGetDouble(out double rd))
        {
            return op switch
            {
                CompareOp.GreaterThan => ld > rd,
                CompareOp.GreaterThanOrEqual => ld >= rd,
                CompareOp.LessThan => ld < rd,
                CompareOp.LessThanOrEqual => ld <= rd,
                _ => false,
            };
        }

        return CompareCoercedElement(left.AsElement(workspace), right.AsElement(workspace), op);
    }

    private static bool CompareCoercedElement(in JsonElement left, in JsonElement right, CompareOp op)
    {
        if (left.IsNullOrUndefined() || right.IsNullOrUndefined())
        {
            return false;
        }

        if (!JsonLogicHelpers.TryCoerceToNumber(left, out JsonElement leftNum)
            || !JsonLogicHelpers.TryCoerceToNumber(right, out JsonElement rightNum))
        {
            return false;
        }

        int cmp = JsonLogicHelpers.CompareNumbers(leftNum, rightNum);
        return op switch
        {
            CompareOp.GreaterThan => cmp > 0,
            CompareOp.GreaterThanOrEqual => cmp >= 0,
            CompareOp.LessThan => cmp < 0,
            CompareOp.LessThanOrEqual => cmp <= 0,
            _ => false,
        };
    }

    // ─── EQUALITY ────────────────────────────────────────────────
    private static RuleEvaluator CompileEquals(RuleEvaluator[] operands)
    {
        if (operands.Length < 2)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
        }

        RuleEvaluator left = operands[0];
        RuleEvaluator right = operands[1];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult lr = left(data, workspace);
            EvalResult rr = right(data, workspace);
            return EvalResult.FromElement(
                JsonLogicHelpers.BooleanElement(CoercingEquals(lr, rr, workspace)));
        };
    }

    private static RuleEvaluator CompileNotEquals(RuleEvaluator[] operands)
    {
        if (operands.Length < 2)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.BooleanElement(true));
        }

        RuleEvaluator left = operands[0];
        RuleEvaluator right = operands[1];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult lr = left(data, workspace);
            EvalResult rr = right(data, workspace);
            return EvalResult.FromElement(
                JsonLogicHelpers.BooleanElement(!CoercingEquals(lr, rr, workspace)));
        };
    }

    private static RuleEvaluator CompileStrictEquals(RuleEvaluator[] operands)
    {
        if (operands.Length < 2)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
        }

        RuleEvaluator left = operands[0];
        RuleEvaluator right = operands[1];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult lr = left(data, workspace);
            EvalResult rr = right(data, workspace);
            return EvalResult.FromElement(
                JsonLogicHelpers.BooleanElement(StrictEquals(lr, rr, workspace)));
        };
    }

    private static RuleEvaluator CompileStrictNotEquals(RuleEvaluator[] operands)
    {
        if (operands.Length < 2)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.BooleanElement(true));
        }

        RuleEvaluator left = operands[0];
        RuleEvaluator right = operands[1];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult lr = left(data, workspace);
            EvalResult rr = right(data, workspace);
            return EvalResult.FromElement(
                JsonLogicHelpers.BooleanElement(!StrictEquals(lr, rr, workspace)));
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CoercingEquals(in EvalResult left, in EvalResult right, JsonWorkspace workspace)
    {
        if (left.IsDouble && right.IsDouble)
        {
            left.TryGetDouble(out double l);
            right.TryGetDouble(out double r);
            return l == r;
        }

        return CoercingEqualsElement(left.AsElement(workspace), right.AsElement(workspace));
    }

    private static bool CoercingEqualsElement(in JsonElement left, in JsonElement right)
    {
        if (left.ValueKind == right.ValueKind)
        {
            return StrictEqualsElement(left, right);
        }

        if (left.IsNullOrUndefined() && right.IsNullOrUndefined())
        {
            return true;
        }

        if (left.IsNullOrUndefined() || right.IsNullOrUndefined())
        {
            return false;
        }

        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.String)
        {
            return JsonLogicHelpers.TryCoerceToNumber(right, out JsonElement rightNum)
                && JsonLogicHelpers.AreNumbersEqual(left, rightNum);
        }

        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.Number)
        {
            return JsonLogicHelpers.TryCoerceToNumber(left, out JsonElement leftNum)
                && JsonLogicHelpers.AreNumbersEqual(leftNum, right);
        }

        if (left.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            JsonElement leftNum = left.ValueKind == JsonValueKind.True
                ? JsonLogicHelpers.One()
                : JsonLogicHelpers.Zero();
            return CoercingEqualsElement(leftNum, right);
        }

        if (right.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            JsonElement rightNum = right.ValueKind == JsonValueKind.True
                ? JsonLogicHelpers.One()
                : JsonLogicHelpers.Zero();
            return CoercingEqualsElement(left, rightNum);
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StrictEquals(in EvalResult left, in EvalResult right, JsonWorkspace workspace)
    {
        if (left.IsDouble && right.IsDouble)
        {
            left.TryGetDouble(out double l);
            right.TryGetDouble(out double r);
            return l == r;
        }

        return StrictEqualsElement(left.AsElement(workspace), right.AsElement(workspace));
    }

    private static bool StrictEqualsElement(in JsonElement left, in JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        if (left.IsNullOrUndefined() && right.IsNullOrUndefined())
        {
            return true;
        }

        if (left.ValueKind == JsonValueKind.Number)
        {
            return JsonLogicHelpers.AreNumbersEqual(left, right);
        }

        if (left.ValueKind == JsonValueKind.String)
        {
            using RawUtf8JsonString leftRaw = JsonMarshal.GetRawUtf8Value(left);
            using RawUtf8JsonString rightRaw = JsonMarshal.GetRawUtf8Value(right);
            return leftRaw.Span.SequenceEqual(rightRaw.Span);
        }

        return left.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }

    // ─── LOGIC ───────────────────────────────────────────────────
    private static RuleEvaluator CompileNot(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.BooleanElement(true));
        }

        RuleEvaluator op = operands[0];
        return (in JsonElement data, JsonWorkspace workspace) =>
            EvalResult.FromElement(JsonLogicHelpers.BooleanElement(!op(data, workspace).IsTruthy()));
    }

    private static RuleEvaluator CompileTruthy(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
        }

        RuleEvaluator op = operands[0];
        return (in JsonElement data, JsonWorkspace workspace) =>
            EvalResult.FromElement(JsonLogicHelpers.BooleanElement(op(data, workspace).IsTruthy()));
    }

    private static RuleEvaluator CompileAnd(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult result = default;
            for (int i = 0; i < operands.Length; i++)
            {
                result = operands[i](data, workspace);
                if (!result.IsTruthy())
                {
                    return result;
                }
            }

            return result;
        };
    }

    private static RuleEvaluator CompileOr(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult result = default;
            for (int i = 0; i < operands.Length; i++)
            {
                result = operands[i](data, workspace);
                if (result.IsTruthy())
                {
                    return result;
                }
            }

            return result;
        };
    }

    private static RuleEvaluator CompileIf(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.NullElement());
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            int i = 0;
            while (i < operands.Length - 1)
            {
                if (operands[i](data, workspace).IsTruthy())
                {
                    return operands[i + 1](data, workspace);
                }

                i += 2;
            }

            // Else clause (odd argument)
            if (i < operands.Length)
            {
                return operands[i](data, workspace);
            }

            return EvalResult.FromElement(JsonLogicHelpers.NullElement());
        };
    }

    // ─── STRING ──────────────────────────────────────────────────
    private static RuleEvaluator CompileCat(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.EmptyString());
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            // Rent a buffer that becomes the FJD's owned buffer — no intermediate copy
            byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
            int pos = 0;
            buffer[pos++] = (byte)'"';

            for (int i = 0; i < operands.Length; i++)
            {
                EvalResult r = operands[i](data, workspace);

                if (r.IsDouble)
                {
                    r.TryGetDouble(out double d);
                    GrowCatBufferIfNeeded(ref buffer, pos, 32);

                    if (Utf8Formatter.TryFormat(d, buffer.AsSpan(pos), out int bytesWritten))
                    {
                        pos += bytesWritten;
                    }
                }
                else
                {
                    AppendCoercedToBuffer(r.Element, ref buffer, ref pos);
                }
            }

            GrowCatBufferIfNeeded(ref buffer, pos, 1);
            buffer[pos++] = (byte)'"';

            // Use the public helper to create the string element from the quoted UTF-8 span
            JsonElement result = JsonLogicHelpers.StringFromQuotedUtf8Span(
                new ReadOnlySpan<byte>(buffer, 0, pos), workspace);
            ArrayPool<byte>.Shared.Return(buffer);
            return EvalResult.FromElement(result);
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendCoercedToBuffer(in JsonElement elem, ref byte[] buffer, ref int pos)
    {
        if (elem.IsNullOrUndefined())
        {
            GrowCatBufferIfNeeded(ref buffer, pos, 4);
            "null"u8.CopyTo(buffer.AsSpan(pos));
            pos += 4;
            return;
        }

        switch (elem.ValueKind)
        {
            case JsonValueKind.String:
            {
                using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(elem);
                ReadOnlySpan<byte> span = raw.Span;
                if (span.Length > 2)
                {
                    int contentLen = span.Length - 2;
                    GrowCatBufferIfNeeded(ref buffer, pos, contentLen);
                    span.Slice(1, contentLen).CopyTo(buffer.AsSpan(pos));
                    pos += contentLen;
                }

                break;
            }

            case JsonValueKind.Number:
            {
                using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(elem);
                ReadOnlySpan<byte> span = raw.Span;
                GrowCatBufferIfNeeded(ref buffer, pos, span.Length);
                span.CopyTo(buffer.AsSpan(pos));
                pos += span.Length;
                break;
            }

            case JsonValueKind.True:
                GrowCatBufferIfNeeded(ref buffer, pos, 4);
                "true"u8.CopyTo(buffer.AsSpan(pos));
                pos += 4;
                break;

            case JsonValueKind.False:
                GrowCatBufferIfNeeded(ref buffer, pos, 5);
                "false"u8.CopyTo(buffer.AsSpan(pos));
                pos += 5;
                break;

            case JsonValueKind.Null:
                GrowCatBufferIfNeeded(ref buffer, pos, 4);
                "null"u8.CopyTo(buffer.AsSpan(pos));
                pos += 4;
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GrowCatBufferIfNeeded(ref byte[] buffer, int pos, int needed)
    {
        if (pos + needed > buffer.Length)
        {
            GrowCatBuffer(ref buffer, pos, needed);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GrowCatBuffer(ref byte[] buffer, int pos, int needed)
    {
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(buffer.Length * 2, pos + needed));
        buffer.AsSpan(0, pos).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = newBuffer;
    }

    private static RuleEvaluator CompileSubstr(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.EmptyString());
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            string? str = JsonLogicHelpers.CoerceToString(operands[0](data, workspace).AsElement(workspace));
            if (str is null)
            {
                return EvalResult.FromElement(JsonLogicHelpers.EmptyString());
            }

            BigNumber startBn = operands.Length > 1
                ? CoerceToBigNumber(operands[1](data, workspace).AsElement(workspace))
                : BigNumber.Zero;
            int start = (int)(long)startBn;
            if (start < 0)
            {
                start = Math.Max(0, str.Length + start);
            }

            if (start >= str.Length)
            {
                return EvalResult.FromElement(JsonLogicHelpers.EmptyString());
            }

            int length;
            if (operands.Length > 2)
            {
                BigNumber lenBn = CoerceToBigNumber(operands[2](data, workspace).AsElement(workspace));
                int lenVal = (int)(long)lenBn;
                length = lenVal < 0 ? Math.Max(0, str.Length - start + lenVal) : lenVal;
            }
            else
            {
                length = str.Length - start;
            }

            length = Math.Min(length, str.Length - start);
            if (length <= 0)
            {
                return EvalResult.FromElement(JsonLogicHelpers.EmptyString());
            }

            return EvalResult.FromElement(JsonLogicHelpers.StringToElement(str.Substring(start, length)));
        };
    }

    // ─── ARRAY ───────────────────────────────────────────────────
    private static RuleEvaluator CompileIn(RuleEvaluator[] operands)
    {
        if (operands.Length < 2)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
        }

        RuleEvaluator needleExpr = operands[0];
        RuleEvaluator haystackExpr = operands[1];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement needle = needleExpr(data, workspace).AsElement(workspace);
            JsonElement haystack = haystackExpr(data, workspace).AsElement(workspace);

            if (haystack.ValueKind == JsonValueKind.String)
            {
                if (needle.ValueKind != JsonValueKind.String)
                {
                    return EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
                }

                using RawUtf8JsonString haystackRaw = JsonMarshal.GetRawUtf8Value(haystack);
                using RawUtf8JsonString needleRaw = JsonMarshal.GetRawUtf8Value(needle);
                ReadOnlySpan<byte> haystackContent = haystackRaw.Span.Length > 2 ? haystackRaw.Span.Slice(1, haystackRaw.Span.Length - 2) : ReadOnlySpan<byte>.Empty;
                ReadOnlySpan<byte> needleContent = needleRaw.Span.Length > 2 ? needleRaw.Span.Slice(1, needleRaw.Span.Length - 2) : ReadOnlySpan<byte>.Empty;
                bool found = haystackContent.IndexOf(needleContent) >= 0;
                return EvalResult.FromElement(JsonLogicHelpers.BooleanElement(found));
            }

            if (haystack.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in haystack.EnumerateArray())
                {
                    if (StrictEqualsElement(needle, item))
                    {
                        return EvalResult.FromElement(JsonLogicHelpers.BooleanElement(true));
                    }
                }
            }

            return EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
        };
    }

    private static RuleEvaluator CompileMerge(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.EmptyArray());
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, operands.Length * 4);
            JsonElement.Mutable root = doc.RootElement;

            for (int i = 0; i < operands.Length; i++)
            {
                JsonElement elem = operands[i](data, workspace).AsElement(workspace);
                if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in elem.EnumerateArray())
                    {
                        root.AddItem(item);
                    }
                }
                else
                {
                    root.AddItem(elem);
                }
            }

            return EvalResult.FromElement((JsonElement)root);
        };
    }

    private static RuleEvaluator CompileMap(in JsonElement args, Dictionary<string, IJsonLogicOperator>? operators)
    {
        RuleEvaluator[] compiled = CompileArgs(args, operators);
        if (compiled.Length < 2)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.EmptyArray());
        }

        RuleEvaluator arrayExpr = compiled[0];
        RuleEvaluator bodyExpr = compiled[1];

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement arr = arrayExpr(data, workspace).AsElement(workspace);
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            {
                return EvalResult.FromElement(JsonLogicHelpers.EmptyArray());
            }

            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, arr.GetArrayLength());
            JsonElement.Mutable root = doc.RootElement;

            foreach (JsonElement item in arr.EnumerateArray())
            {
                root.AddItem(bodyExpr(item, workspace).AsElement(workspace));
            }

            return EvalResult.FromElement((JsonElement)root);
        };
    }

    private static RuleEvaluator CompileFilter(in JsonElement args, Dictionary<string, IJsonLogicOperator>? operators)
    {
        RuleEvaluator[] compiled = CompileArgs(args, operators);
        if (compiled.Length < 2)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.EmptyArray());
        }

        RuleEvaluator arrayExpr = compiled[0];
        RuleEvaluator predicateExpr = compiled[1];

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement arr = arrayExpr(data, workspace).AsElement(workspace);
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            {
                return EvalResult.FromElement(JsonLogicHelpers.EmptyArray());
            }

            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, arr.GetArrayLength());
            JsonElement.Mutable root = doc.RootElement;

            foreach (JsonElement item in arr.EnumerateArray())
            {
                if (predicateExpr(item, workspace).IsTruthy())
                {
                    root.AddItem(item);
                }
            }

            return EvalResult.FromElement((JsonElement)root);
        };
    }

    private static RuleEvaluator CompileReduce(in JsonElement args, Dictionary<string, IJsonLogicOperator>? operators)
    {
        if (args.ValueKind != JsonValueKind.Array || args.GetArrayLength() < 3)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.NullElement());
        }

        JsonElement arrayArgRule = args[0];
        JsonElement bodyRule = args[1];
        JsonElement initRule = args[2];

        // Check if the reduce body only references "current" and "accumulator".
        // When true, we can avoid constructing a JSON object for the data context
        // and instead pass EvalResult values directly through a captured ReduceContext.
        bool canOptimize = BodyUsesOnlyReduceVars(bodyRule);

        if (canOptimize)
        {
            // Check for map-reduce fusion: reduce(map(arr, mapBody), reduceBody, init)
            // Fuses into a single loop, eliminating the intermediate array entirely.
            if (TryGetMapArgs(arrayArgRule, out JsonElement mapArrayRule, out JsonElement mapBodyRule))
            {
                return CompileFusedMapReduce(mapArrayRule, mapBodyRule, bodyRule, initRule, operators);
            }

            return CompileOptimizedReduce(arrayArgRule, bodyRule, initRule, operators);
        }

        // Fallback: use JSON object context (handles var "", dotted paths, etc.)
        return CompileFallbackReduce(args, operators);
    }

    private static RuleEvaluator CompileOptimizedReduce(
        in JsonElement arrayArgRule,
        in JsonElement bodyRule,
        in JsonElement initRule,
        Dictionary<string, IJsonLogicOperator>? operators)
    {
        RuleEvaluator arrayExpr = CompileExpression(arrayArgRule, operators);
        RuleEvaluator initExpr = CompileExpression(initRule, operators);

        ReduceContext ctx = new();
        ReduceContext? saved = t_reduceContext;
        t_reduceContext = ctx;
        RuleEvaluator bodyExpr = CompileExpression(bodyRule, operators);
        t_reduceContext = saved;

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement arr = arrayExpr(data, workspace).AsElement(workspace);
            EvalResult accumulator = initExpr(data, workspace);

            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            {
                return accumulator;
            }

            foreach (JsonElement item in arr.EnumerateArray())
            {
                ctx.Current = EvalResult.FromElement(item);
                ctx.Accumulator = accumulator;
                accumulator = bodyExpr(data, workspace);
            }

            return accumulator;
        };
    }

    private static RuleEvaluator CompileFusedMapReduce(
        in JsonElement mapArrayRule,
        in JsonElement mapBodyRule,
        in JsonElement reduceBodyRule,
        in JsonElement initRule,
        Dictionary<string, IJsonLogicOperator>? operators)
    {
        RuleEvaluator arrayExpr = CompileExpression(mapArrayRule, operators);
        RuleEvaluator mapBody = CompileExpression(mapBodyRule, operators);
        RuleEvaluator initExpr = CompileExpression(initRule, operators);

        ReduceContext ctx = new();
        ReduceContext? saved = t_reduceContext;
        t_reduceContext = ctx;
        RuleEvaluator reduceBody = CompileExpression(reduceBodyRule, operators);
        t_reduceContext = saved;

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement arr = arrayExpr(data, workspace).AsElement(workspace);
            EvalResult accumulator = initExpr(data, workspace);

            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            {
                return accumulator;
            }

            foreach (JsonElement item in arr.EnumerateArray())
            {
                // Apply map body, then feed directly into reduce — no intermediate array.
                EvalResult mapped = mapBody(item, workspace);
                ctx.Current = mapped;
                ctx.Accumulator = accumulator;
                accumulator = reduceBody(data, workspace);
            }

            return accumulator;
        };
    }

    private static RuleEvaluator CompileFallbackReduce(in JsonElement args, Dictionary<string, IJsonLogicOperator>? operators)
    {
        RuleEvaluator[] compiled = CompileArgs(args, operators);
        if (compiled.Length < 3)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.NullElement());
        }

        RuleEvaluator arrayExpr = compiled[0];
        RuleEvaluator bodyExpr = compiled[1];
        RuleEvaluator initExpr = compiled[2];

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement arr = arrayExpr(data, workspace).AsElement(workspace);
            EvalResult accumulator = initExpr(data, workspace);

            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            {
                return accumulator;
            }

            JsonDocumentBuilder<JsonElement.Mutable> contextDoc =
                JsonElement.CreateObjectBuilder(workspace, 2);
            JsonElement.Mutable root = contextDoc.RootElement;

            foreach (JsonElement item in arr.EnumerateArray())
            {
                root.SetProperty("current", item);
                root.SetProperty("accumulator", accumulator.AsElement(workspace));
                accumulator = bodyExpr((JsonElement)root, workspace);
            }

            return accumulator;
        };
    }

    private enum QuantifierKind
    {
        All,
        None,
        Some,
    }

    private static RuleEvaluator CompileQuantifier(in JsonElement args, Dictionary<string, IJsonLogicOperator>? operators, QuantifierKind kind)
    {
        RuleEvaluator[] compiled = CompileArgs(args, operators);
        if (compiled.Length < 2)
        {
            return (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.BooleanElement(kind == QuantifierKind.All || kind == QuantifierKind.None));
        }

        RuleEvaluator arrayExpr = compiled[0];
        RuleEvaluator predicateExpr = compiled[1];

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement arr = arrayExpr(data, workspace).AsElement(workspace);
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            {
                bool emptyResult = kind == QuantifierKind.None;
                return EvalResult.FromElement(JsonLogicHelpers.BooleanElement(emptyResult));
            }

            foreach (JsonElement item in arr.EnumerateArray())
            {
                bool truthy = predicateExpr(item, workspace).IsTruthy();
                switch (kind)
                {
                    case QuantifierKind.All when !truthy:
                        return EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
                    case QuantifierKind.None when truthy:
                        return EvalResult.FromElement(JsonLogicHelpers.BooleanElement(false));
                    case QuantifierKind.Some when truthy:
                        return EvalResult.FromElement(JsonLogicHelpers.BooleanElement(true));
                }
            }

            bool defaultResult = kind != QuantifierKind.Some;
            return EvalResult.FromElement(JsonLogicHelpers.BooleanElement(defaultResult));
        };
    }

    // ─── DATA ────────────────────────────────────────────────────
    private static RuleEvaluator CompileMissing(RuleEvaluator[] operands)
    {
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, operands.Length);
            JsonElement.Mutable root = doc.RootElement;

            for (int i = 0; i < operands.Length; i++)
            {
                JsonElement path = operands[i](data, workspace).AsElement(workspace);
                if (path.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in path.EnumerateArray())
                    {
                        JsonElement resolved = ResolveVar(data, item);
                        if (resolved.IsNullOrUndefined())
                        {
                            root.AddItem(item);
                        }
                    }
                }
                else
                {
                    JsonElement resolved = ResolveVar(data, path);
                    if (resolved.IsNullOrUndefined())
                    {
                        root.AddItem(path);
                    }
                }
            }

            return EvalResult.FromElement((JsonElement)root);
        };
    }

    private static RuleEvaluator CompileMissingSome(in JsonElement args, Dictionary<string, IJsonLogicOperator>? operators)
    {
        RuleEvaluator[] compiled = CompileArgs(args, operators);
        if (compiled.Length < 2)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.EmptyArray());
        }

        RuleEvaluator neededExpr = compiled[0];
        RuleEvaluator pathsExpr = compiled[1];

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement neededElem = neededExpr(data, workspace).AsElement(workspace);
            int needed = 0;
            if (neededElem.ValueKind == JsonValueKind.Number && neededElem.TryGetDouble(out double nd))
            {
                needed = (int)nd;
            }

            JsonElement paths = pathsExpr(data, workspace).AsElement(workspace);
            if (paths.ValueKind != JsonValueKind.Array)
            {
                return EvalResult.FromElement(JsonLogicHelpers.EmptyArray());
            }

            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, paths.GetArrayLength());
            JsonElement.Mutable root = doc.RootElement;

            int found = 0;
            foreach (JsonElement path in paths.EnumerateArray())
            {
                JsonElement resolved = ResolveVar(data, path);
                if (resolved.IsNullOrUndefined())
                {
                    root.AddItem(path);
                }
                else
                {
                    found++;
                }
            }

            if (found >= needed)
            {
                return EvalResult.FromElement(JsonLogicHelpers.EmptyArray());
            }

            return EvalResult.FromElement((JsonElement)root);
        };
    }

    // ─── MISC ────────────────────────────────────────────────────
    private static RuleEvaluator CompileLog(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.NullElement());
        }

        RuleEvaluator op = operands[0];
        return (in JsonElement data, JsonWorkspace workspace) => op(data, workspace);
    }

    private static RuleEvaluator CompileAsDouble(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.Zero());
        }

        RuleEvaluator op = operands[0];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult r = op(data, workspace);
            if (r.TryGetDouble(out double d))
            {
                return EvalResult.FromDouble(d);
            }

            JsonElement elem = r.AsElement(workspace);
            return elem.TryGetDouble(out double val)
                ? EvalResult.FromDouble(val)
                : EvalResult.FromElement(JsonLogicHelpers.NullElement());
        };
    }

    private static RuleEvaluator CompileAsLong(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.Zero());
        }

        RuleEvaluator op = operands[0];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult r = op(data, workspace);
            if (r.TryGetDouble(out double d))
            {
                return EvalResult.FromDouble((long)d);
            }

            JsonElement elem = r.AsElement(workspace);
            return elem.TryGetDouble(out double val)
                ? EvalResult.FromDouble((long)val)
                : EvalResult.FromElement(JsonLogicHelpers.NullElement());
        };
    }

    private static RuleEvaluator CompileAsBigNumber(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.Zero());
        }

        RuleEvaluator op = operands[0];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement elem = op(data, workspace).AsElement(workspace);
            BigNumber bn = CoerceToBigNumber(elem);
            return EvalResult.FromElement(BigNumberToElement(bn, workspace));
        };
    }

    private static RuleEvaluator CompileAsBigInteger(RuleEvaluator[] operands)
    {
        if (operands.Length == 0)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromElement(JsonLogicHelpers.Zero());
        }

        RuleEvaluator op = operands[0];
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement elem = op(data, workspace).AsElement(workspace);
            BigNumber bn = CoerceToBigNumber(elem);
            bn = BigNumber.Truncate(bn);
            return EvalResult.FromElement(BigNumberToElement(bn, workspace));
        };
    }

    // ─── SHARED HELPERS ──────────────────────────────────────────
    private static JsonElement ResolveVar(in JsonElement data, in JsonElement pathElement)
    {
        if (pathElement.IsNullOrUndefined())
        {
            return data;
        }

        if (pathElement.ValueKind == JsonValueKind.Number)
        {
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(pathElement);
            return WalkPathUtf8(data, raw.Span);
        }

        if (pathElement.ValueKind == JsonValueKind.String)
        {
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(pathElement);
            ReadOnlySpan<byte> quoted = raw.Span;
            if (quoted.Length <= 2)
            {
                return data;
            }

            return WalkPathUtf8(data, quoted.Slice(1, quoted.Length - 2));
        }

        return JsonLogicHelpers.NullElement();
    }

    private static JsonElement WalkPathUtf8(JsonElement current, ReadOnlySpan<byte> path)
    {
        while (path.Length > 0)
        {
            if (current.IsNullOrUndefined())
            {
                return JsonLogicHelpers.NullElement();
            }

            int dotIndex = path.IndexOf((byte)'.');
            ReadOnlySpan<byte> segment = dotIndex >= 0 ? path.Slice(0, dotIndex) : path;
            path = dotIndex >= 0 ? path.Slice(dotIndex + 1) : ReadOnlySpan<byte>.Empty;

            if (current.ValueKind == JsonValueKind.Array)
            {
                if (TryParseIndexUtf8(segment, out int index) && index >= 0 && index < current.GetArrayLength())
                {
                    current = current[index];
                }
                else
                {
                    return JsonLogicHelpers.NullElement();
                }
            }
            else if (current.ValueKind == JsonValueKind.Object)
            {
                if (current.TryGetProperty(segment, out JsonElement prop))
                {
                    current = prop;
                }
                else
                {
                    return JsonLogicHelpers.NullElement();
                }
            }
            else
            {
                return JsonLogicHelpers.NullElement();
            }
        }

        return current;
    }

    private static bool TryParseIndexUtf8(ReadOnlySpan<byte> utf8, out int value)
    {
        value = 0;
        if (utf8.Length == 0 || utf8.Length > 10)
        {
            return false;
        }

        for (int i = 0; i < utf8.Length; i++)
        {
            byte b = utf8[i];
            if (b < (byte)'0' || b > (byte)'9')
            {
                return false;
            }

            value = (value * 10) + (b - '0');
        }

        return true;
    }

    private static bool TryParseIndexUtf8(byte[] utf8, out int value)
    {
        return TryParseIndexUtf8((ReadOnlySpan<byte>)utf8, out value);
    }

    private static bool TryCoerceToDouble(in JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDouble(out value);
        }

        if (element.ValueKind == JsonValueKind.True)
        {
            value = 1;
            return true;
        }

        if (element.ValueKind == JsonValueKind.False || element.ValueKind == JsonValueKind.Null)
        {
            value = 0;
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            if (JsonLogicHelpers.TryCoerceToNumber(element, out JsonElement numElem) && numElem.TryGetDouble(out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        value = 0;
        return false;
    }

    private static BigNumber CoerceToBigNumber(in JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(element);
            if (BigNumber.TryParse(raw.Span, out BigNumber result))
            {
                return result;
            }
        }

        if (element.ValueKind == JsonValueKind.True)
        {
            return BigNumber.One;
        }

        if (element.ValueKind == JsonValueKind.False || element.ValueKind == JsonValueKind.Null)
        {
            return BigNumber.Zero;
        }

        if (element.ValueKind == JsonValueKind.String && JsonLogicHelpers.TryCoerceToNumber(element, out JsonElement numElem))
        {
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(numElem);
            if (BigNumber.TryParse(raw.Span, out BigNumber result))
            {
                return result;
            }
        }

        return BigNumber.Zero;
    }

    private static JsonElement BigNumberToElement(BigNumber value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (value.TryFormat(buffer, out int bytesWritten))
        {
            return JsonLogicHelpers.NumberFromSpan(buffer.Slice(0, bytesWritten), workspace);
        }

        return JsonLogicHelpers.Zero();
    }

    private static JsonElement DoubleToElement(double value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (Utf8Formatter.TryFormat(value, buffer, out int bytesWritten))
        {
            return JsonLogicHelpers.NumberFromSpan(buffer.Slice(0, bytesWritten), workspace);
        }

        return JsonLogicHelpers.Zero();
    }

    // ─── REDUCE OPTIMIZATION HELPERS ────────────────────────────────

    /// <summary>
    /// Mutable context passed to a reduce body's compiled var closures.
    /// Set before each iteration; read by the closures for "current"/"accumulator".
    /// </summary>
    private sealed class ReduceContext
    {
        public EvalResult Current;
        public EvalResult Accumulator;
    }

    /// <summary>
    /// Checks whether a reduce body rule only references var "current" and
    /// var "accumulator" (no other vars, no var "", no dotted paths).
    /// When true, the ReduceContext optimization can be safely applied.
    /// </summary>
    private static bool BodyUsesOnlyReduceVars(in JsonElement rule)
    {
        if (rule.ValueKind == JsonValueKind.Object && rule.GetPropertyCount() > 0)
        {
            foreach (JsonProperty<JsonElement> prop in rule.EnumerateObject())
            {
                if (prop.Name == "var")
                {
                    return IsReduceVarPath(prop.Value);
                }

                // Non-var operator: recurse into its arguments
                return BodyUsesOnlyReduceVars(prop.Value);
            }
        }

        if (rule.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in rule.EnumerateArray())
            {
                if (!BodyUsesOnlyReduceVars(item))
                {
                    return false;
                }
            }

            return true;
        }

        // Literals are always safe
        return true;
    }

    private static bool IsReduceVarPath(in JsonElement args)
    {
        JsonElement pathArg = args;
        if (args.ValueKind == JsonValueKind.Array)
        {
            if (args.GetArrayLength() == 0)
            {
                return false;
            }

            pathArg = args[0];
        }

        if (pathArg.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (JsonLogicHelpers.IsEmptyString(pathArg))
        {
            return false;
        }

        byte[][] segments = PrecomputePathSegments(pathArg);
        if (segments.Length != 1)
        {
            return false;
        }

        ReadOnlySpan<byte> seg = segments[0];
        return seg.SequenceEqual("current"u8) || seg.SequenceEqual("accumulator"u8);
    }

    /// <summary>
    /// Checks whether a rule element is a {"map":[arrayExpr, bodyExpr]} and extracts the args.
    /// </summary>
    private static bool TryGetMapArgs(in JsonElement rule, out JsonElement arrayArg, out JsonElement bodyArg)
    {
        arrayArg = default;
        bodyArg = default;

        if (rule.ValueKind != JsonValueKind.Object || rule.GetPropertyCount() != 1)
        {
            return false;
        }

        foreach (JsonProperty<JsonElement> prop in rule.EnumerateObject())
        {
            if (prop.Name != "map")
            {
                return false;
            }

            JsonElement mapArgs = prop.Value;
            if (mapArgs.ValueKind != JsonValueKind.Array || mapArgs.GetArrayLength() < 2)
            {
                return false;
            }

            arrayArg = mapArgs[0];
            bodyArg = mapArgs[1];
            return true;
        }

        return false;
    }
}