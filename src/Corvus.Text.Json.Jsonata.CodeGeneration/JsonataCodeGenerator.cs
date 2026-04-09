// <copyright file="JsonataCodeGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Jsonata.Ast;

namespace Corvus.Text.Json.Jsonata.CodeGeneration;

/// <summary>
/// Generates static C# evaluation code from a JSONata expression string.
/// </summary>
/// <remarks>
/// <para>
/// The generator walks the JSONata AST and emits inline C# code that calls
/// <c>JsonataCodeGenHelpers</c> methods. For expressions containing constructs
/// that cannot be statically compiled (closures, parent operator, transforms, etc.),
/// the generator falls back to a runtime wrapper that delegates to
/// <see cref="JsonataEvaluator"/>.
/// </para>
/// </remarks>
public static class JsonataCodeGenerator
{
    private const string H = "JsonataCodeGenHelpers";

    /// <summary>
    /// Generates a complete C# source file containing a static class with an
    /// <c>Evaluate</c> method that evaluates the given JSONata expression.
    /// </summary>
    /// <param name="expression">The JSONata expression string.</param>
    /// <param name="className">The name of the generated static class.</param>
    /// <param name="namespaceName">The namespace for the generated class.</param>
    /// <returns>A complete C# source file as a string.</returns>
    /// <exception cref="JsonataException">
    /// Thrown if <paramref name="expression"/> is not a valid JSONata expression.
    /// </exception>
    public static string Generate(string expression, string className, string namespaceName)
    {
        JsonataNode ast = Parser.Parse(expression);
        Emitter emitter = new();
        StringBuilder body = new(4096);

        try
        {
            string resultVar = emitter.EmitExpression(body, ast, "        ", "data", "workspace");
            return AssembleInlineCode(expression, className, namespaceName, emitter, body, resultVar);
        }
        catch (FallbackException)
        {
            return GenerateRuntimeFallback(expression, className, namespaceName);
        }
    }

    private static string AssembleInlineCode(
        string expression,
        string className,
        string namespaceName,
        Emitter emitter,
        StringBuilder body,
        string resultVar)
    {
        StringBuilder sb = new(8192);

        EmitHeader(sb);

        if (!string.IsNullOrEmpty(namespaceName))
        {
            L(sb, string.Empty, $"namespace {namespaceName};");
            Blank(sb);
        }

        L(sb, string.Empty, $"internal static class {className}");
        L(sb, string.Empty, "{");

        // Static fields
        foreach (string field in emitter.StaticFieldDeclarations)
        {
            L(sb, "    ", field);
        }

        string escaped = EscapeStringLiteral(expression);
        L(sb, "    ", $"private const string Expression = \"{escaped}\";");
        L(sb, "    ", "private static readonly JsonataEvaluator s_evaluator = new();");
        Blank(sb);

        // Primary Evaluate overload - inline code
        EmitXmlDoc(sb, "    ", expression);
        L(sb, string.Empty, "#if !NETFRAMEWORK");
        L(sb, "    ", "[MethodImpl(MethodImplOptions.AggressiveOptimization)]");
        L(sb, string.Empty, "#endif");
        L(sb, "    ", "public static JsonElement Evaluate(in JsonElement data, JsonWorkspace workspace)");
        L(sb, "    ", "{");
        sb.Append(body);
        L(sb, "        ", $"return {resultVar};");
        L(sb, "    ", "}");

        // Bindings overload - runtime fallback
        Blank(sb);
        EmitBindingsOverload(sb);

        sb.Append('}');
        return sb.ToString();
    }

    private static string GenerateRuntimeFallback(string expression, string className, string namespaceName)
    {
        StringBuilder sb = new(4096);

        EmitHeader(sb);

        if (!string.IsNullOrEmpty(namespaceName))
        {
            L(sb, string.Empty, $"namespace {namespaceName};");
            Blank(sb);
        }

        L(sb, string.Empty, $"internal static class {className}");
        L(sb, string.Empty, "{");

        string escaped = EscapeStringLiteral(expression);
        L(sb, "    ", $"private const string Expression = \"{escaped}\";");
        L(sb, "    ", "private static readonly JsonataEvaluator s_evaluator = new();");
        Blank(sb);

        EmitXmlDoc(sb, "    ", expression);
        L(sb, string.Empty, "#if !NETFRAMEWORK");
        L(sb, "    ", "[MethodImpl(MethodImplOptions.AggressiveOptimization)]");
        L(sb, string.Empty, "#endif");
        L(sb, "    ", "public static JsonElement Evaluate(in JsonElement data, JsonWorkspace workspace)");
        L(sb, "    ", "{");
        L(sb, "        ", "return s_evaluator.Evaluate(Expression, data, workspace);");
        L(sb, "    ", "}");

        Blank(sb);
        EmitBindingsOverload(sb);

        sb.Append('}');
        return sb.ToString();
    }

    private static void EmitHeader(StringBuilder sb)
    {
        L(sb, string.Empty, "// <auto-generated/>");
        L(sb, string.Empty, "#nullable enable");
        L(sb, string.Empty, "using System.Runtime.CompilerServices;");
        L(sb, string.Empty, "using Corvus.Text.Json;");
        L(sb, string.Empty, "using Corvus.Text.Json.Jsonata;");
        Blank(sb);
    }

    private static void EmitXmlDoc(StringBuilder sb, string indent, string expression)
    {
        L(sb, indent, "/// <summary>");
        L(sb, indent, $"/// Evaluates the JSONata expression: <c>{EscapeXmlContent(expression)}</c>.");
        L(sb, indent, "/// </summary>");
        L(sb, indent, "/// <param name=\"data\">The input JSON data element.</param>");
        L(sb, indent, "/// <param name=\"workspace\">The workspace for pooled memory.</param>");
        L(sb, indent, "/// <returns>The result of the evaluation, or a <c>default</c> <see cref=\"JsonElement\"/> if the expression produces no result.</returns>");
    }

    private static void EmitBindingsOverload(StringBuilder sb)
    {
        L(sb, "    ", "/// <summary>");
        L(sb, "    ", "/// Evaluates the JSONata expression with external variable bindings and resource limits.");
        L(sb, "    ", "/// </summary>");
        L(sb, "    ", "/// <param name=\"data\">The input JSON data element.</param>");
        L(sb, "    ", "/// <param name=\"workspace\">The workspace for pooled memory.</param>");
        L(sb, "    ", "/// <param name=\"bindings\">Optional external variable bindings.</param>");
        L(sb, "    ", "/// <param name=\"maxDepth\">Maximum evaluation depth (default 500).</param>");
        L(sb, "    ", "/// <param name=\"timeLimitMs\">Time limit in milliseconds (0 = unlimited).</param>");
        L(sb, "    ", "/// <returns>The result of the evaluation, or a <c>default</c> <see cref=\"JsonElement\"/> if the expression produces no result.</returns>");
        L(sb, string.Empty, "#if !NETFRAMEWORK");
        L(sb, "    ", "[MethodImpl(MethodImplOptions.AggressiveOptimization)]");
        L(sb, string.Empty, "#endif");
        L(sb, "    ", "public static JsonElement Evaluate(");
        L(sb, "        ", "in JsonElement data,");
        L(sb, "        ", "JsonWorkspace workspace,");
        L(sb, "        ", "System.Collections.Generic.IReadOnlyDictionary<string, JsonElement>? bindings,");
        L(sb, "        ", "int maxDepth = 500,");
        L(sb, "        ", "int timeLimitMs = 0)");
        L(sb, "    ", "{");
        L(sb, "        ", "return s_evaluator.Evaluate(Expression, data, workspace, bindings, maxDepth, timeLimitMs);");
        L(sb, "    ", "}");
    }

    private static string EscapeStringLiteral(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    private static string EscapeXmlContent(string s)
    {
        return s
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\r", string.Empty)
            .Replace("\n", " ");
    }

    private static void L(StringBuilder sb, string indent, string line)
    {
        sb.Append(indent).Append(line).Append('\n');
    }

    private static void Blank(StringBuilder sb)
    {
        sb.Append('\n');
    }

    /// <summary>
    /// Thrown when the emitter encounters an AST construct that cannot be statically compiled.
    /// Caught by <see cref="Generate"/> to fall back to the runtime wrapper.
    /// </summary>
    private sealed class FallbackException : Exception
    {
        internal FallbackException()
        {
        }
    }

    /// <summary>
    /// Walks the JSONata AST and emits C# statements that evaluate the expression
    /// using <c>JsonataCodeGenHelpers</c> methods.
    /// </summary>
    private sealed class Emitter
    {
        private readonly Dictionary<string, string> _nameFields = new(StringComparer.Ordinal);
        private readonly List<string> _staticFieldDeclarations = new();
        private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);
        private int _varCounter;
        private int _nameFieldCounter;
        private int _pathFieldCounter;
        private int _lambdaCounter;

        /// <summary>Gets the static field declarations collected during emission.</summary>
        internal IReadOnlyList<string> StaticFieldDeclarations => _staticFieldDeclarations;

        internal string NextVar() => $"v{_varCounter++}";

        /// <summary>
        /// Gets or creates a static <c>byte[]</c> field for a property name.
        /// </summary>
        internal string GetOrCreateNameField(string name)
        {
            if (_nameFields.TryGetValue(name, out string? existing))
            {
                return existing;
            }

            string fieldName = $"s_n{_nameFieldCounter++}";
            _nameFields[name] = fieldName;
            _staticFieldDeclarations.Add(
                $"private static readonly byte[] {fieldName} = {H}.Utf8(\"{EscapeStringLiteral(name)}\");");
            return fieldName;
        }

        /// <summary>
        /// Creates a static <c>byte[][]</c> field for a property chain.
        /// </summary>
        internal string CreatePathField(string[] names)
        {
            string fieldName = $"s_p{_pathFieldCounter++}";
            string[] fieldRefs = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                fieldRefs[i] = GetOrCreateNameField(names[i]);
            }

            _staticFieldDeclarations.Add(
                $"private static readonly byte[][] {fieldName} = new byte[][] {{ {string.Join(", ", fieldRefs)} }};");
            return fieldName;
        }

        /// <summary>
        /// Emits C# statements for the given AST node and returns the variable name
        /// holding the result.
        /// </summary>
        internal string EmitExpression(
            StringBuilder sb, JsonataNode node, string indent, string dataVar, string wsVar)
        {
            return node switch
            {
                PathNode path => EmitPath(sb, path, indent, dataVar, wsVar),
                BinaryNode binary => EmitBinary(sb, binary, indent, dataVar, wsVar),
                UnaryNode unary => EmitUnary(sb, unary, indent, dataVar, wsVar),
                NumberNode num => EmitNumber(sb, num, indent, wsVar),
                StringNode str => EmitString(sb, str, indent, wsVar),
                ValueNode val => EmitValue(sb, val, indent),
                NameNode name => EmitName(sb, name, indent, dataVar, wsVar),
                VariableNode variable => EmitVariable(sb, variable, indent, dataVar),
                ConditionNode cond => EmitCondition(sb, cond, indent, dataVar, wsVar),
                BlockNode block => EmitBlock(sb, block, indent, dataVar, wsVar),
                BindNode bind => EmitBind(sb, bind, indent, dataVar, wsVar),
                ArrayConstructorNode arr => EmitArrayConstructor(sb, arr, indent, dataVar, wsVar),
                ObjectConstructorNode obj => EmitObjectConstructor(sb, obj, indent, dataVar, wsVar),
                FunctionCallNode func => EmitFunctionCall(sb, func, indent, dataVar, wsVar),
                FilterNode filter => EmitFilter(sb, filter, indent, dataVar, wsVar),
                _ => throw new FallbackException(),
            };
        }

        // ── Path ─────────────────────────────────────────────
        private string EmitPath(
            StringBuilder sb, PathNode path, string indent, string dataVar, string wsVar)
        {
            List<JsonataNode> steps = path.Steps;

            if (steps.Count == 0)
            {
                return dataVar;
            }

            // Check for simple property chain (all NameNode, no annotations)
            if (IsSimplePropertyChain(steps))
            {
                return EmitSimplePropertyChain(sb, steps, indent, dataVar, wsVar);
            }

            // Process step by step
            string currentVar = dataVar;
            int i = 0;

            // Handle leading VariableNode
            if (steps[0] is VariableNode leadVar)
            {
                currentVar = EmitVariable(sb, leadVar, indent, dataVar);
                i = 1;
            }

            while (i < steps.Count)
            {
                JsonataNode step = steps[i];

                if (step is NameNode)
                {
                    // Collect consecutive unannotated name steps
                    int segStart = i;
                    while (i < steps.Count
                           && steps[i] is NameNode
                           && !HasStages(steps[i])
                           && !HasComplexAnnotations(steps[i]))
                    {
                        i++;
                    }

                    if (i > segStart)
                    {
                        currentVar = EmitPropertyChainSegment(sb, steps, segStart, i, indent, currentVar, wsVar);
                    }

                    // If the next step is a NameNode with filter stages, handle it
                    if (i < steps.Count && steps[i] is NameNode annotated && HasStages(annotated))
                    {
                        if (HasComplexAnnotations(annotated))
                        {
                            throw new FallbackException();
                        }

                        // Navigate the single annotated name
                        string nameField = GetOrCreateNameField(annotated.Value);
                        string navVar = NextVar();
                        L(sb, indent, $"var {navVar} = {H}.NavigateProperty({currentVar}, {nameField}, {wsVar});");
                        currentVar = navVar;

                        // Apply filter stages
                        foreach (JsonataNode stage in annotated.Annotations!.Stages)
                        {
                            currentVar = EmitFilterStage(sb, stage, currentVar, indent, wsVar);
                        }

                        // Apply group-by if present
                        if (annotated.Annotations.Group is not null)
                        {
                            currentVar = EmitGroupByAnnotation(sb, annotated.Annotations.Group, currentVar, indent, wsVar);
                        }

                        i++;
                    }
                }
                else if (step is ObjectConstructorNode objCtor)
                {
                    currentVar = EmitGroupByStep(sb, objCtor, currentVar, indent, wsVar);
                    i++;
                }
                else
                {
                    // Computed step (binary, function call, etc.) -> map over elements
                    currentVar = EmitComputedStep(sb, step, currentVar, indent, wsVar);
                    i++;
                }
            }

            return currentVar;
        }

        private static bool IsSimplePropertyChain(List<JsonataNode> steps)
        {
            foreach (JsonataNode step in steps)
            {
                if (step is not NameNode)
                {
                    return false;
                }

                if (HasStages(step) || HasComplexAnnotations(step))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasStages(JsonataNode node)
        {
            return node.Annotations?.Stages is { Count: > 0 };
        }

        private static bool HasComplexAnnotations(JsonataNode node)
        {
            StepAnnotations? ann = node.Annotations;
            if (ann is null)
            {
                return false;
            }

            return ann.Focus is not null
                   || ann.Index is not null
                   || ann.AncestorLabels is not null
                   || ann.TupleLabels is not null
                   || ann.Tuple;
        }

        private string EmitSimplePropertyChain(
            StringBuilder sb, List<JsonataNode> steps, string indent, string dataVar, string wsVar)
        {
            if (steps.Count == 1)
            {
                string nameField = GetOrCreateNameField(((NameNode)steps[0]).Value);
                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.NavigateProperty({dataVar}, {nameField}, {wsVar});");
                return v;
            }

            string[] names = new string[steps.Count];
            for (int i = 0; i < steps.Count; i++)
            {
                names[i] = ((NameNode)steps[i]).Value;
            }

            string pathField = CreatePathField(names);
            string v2 = NextVar();
            L(sb, indent, $"var {v2} = {H}.NavigatePropertyChain({dataVar}, {pathField}, {wsVar});");
            return v2;
        }

        private string EmitPropertyChainSegment(
            StringBuilder sb, List<JsonataNode> steps, int start, int end,
            string indent, string currentVar, string wsVar)
        {
            int count = end - start;
            if (count == 1)
            {
                string nameField = GetOrCreateNameField(((NameNode)steps[start]).Value);
                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.NavigateProperty({currentVar}, {nameField}, {wsVar});");
                return v;
            }

            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = ((NameNode)steps[start + i]).Value;
            }

            string pathField = CreatePathField(names);
            string v2 = NextVar();
            L(sb, indent, $"var {v2} = {H}.NavigatePropertyChain({currentVar}, {pathField}, {wsVar});");
            return v2;
        }

        private string EmitFilterStage(
            StringBuilder sb, JsonataNode stage, string currentVar, string indent, string wsVar)
        {
            if (stage is not FilterNode filter)
            {
                throw new FallbackException();
            }

            // Numeric index -> ArrayIndex
            if (filter.Expression is NumberNode numIdx)
            {
                int idx = (int)numIdx.Value;
                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.ArrayIndex({currentVar}, {idx});");
                return v;
            }

            // General predicate filter
            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "    ";
            StringBuilder lambdaBody = new();

            string predicateVar = EmitExpression(lambdaBody, filter.Expression, innerIndent, elParam, wsParam);

            string v2 = NextVar();
            L(sb, indent, $"var {v2} = {H}.FilterElements({currentVar}, static (JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent, "{");
            sb.Append(lambdaBody);
            L(sb, innerIndent, $"return {H}.IsTruthy({predicateVar});");
            L(sb, indent, $"}}, {wsVar});");
            return v2;
        }

        private string EmitGroupByStep(
            StringBuilder sb, ObjectConstructorNode objCtor, string currentVar,
            string indent, string wsVar)
        {
            if (objCtor.Pairs.Count != 1)
            {
                throw new FallbackException();
            }

            (JsonataNode keyExpr, JsonataNode valExpr) = objCtor.Pairs[0];

            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "        ";

            StringBuilder keySb = new();
            string keyVar = EmitExpression(keySb, keyExpr, innerIndent, elParam, wsParam);

            StringBuilder valSb = new();
            string valVar = EmitExpression(valSb, valExpr, innerIndent, elParam, wsParam);

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.GroupByObject({currentVar},");
            L(sb, indent + "    ", $"static (JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent + "    ", "{");
            sb.Append(keySb);
            L(sb, innerIndent, $"return {keyVar}.ValueKind == JsonValueKind.String ? {keyVar}.GetString() : null;");
            L(sb, indent + "    ", "},");
            L(sb, indent + "    ", $"static (JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent + "    ", "{");
            sb.Append(valSb);
            L(sb, innerIndent, $"return {valVar};");
            L(sb, indent + "    ", "},");
            L(sb, indent + "    ", $"{wsVar});");
            return v;
        }

        private string EmitGroupByAnnotation(
            StringBuilder sb, GroupBy group, string currentVar, string indent, string wsVar)
        {
            if (group.Pairs.Count != 1)
            {
                throw new FallbackException();
            }

            (JsonataNode keyExpr, JsonataNode valExpr) = group.Pairs[0];

            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "        ";

            StringBuilder keySb = new();
            string keyVar = EmitExpression(keySb, keyExpr, innerIndent, elParam, wsParam);

            StringBuilder valSb = new();
            string valVar = EmitExpression(valSb, valExpr, innerIndent, elParam, wsParam);

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.GroupByObject({currentVar},");
            L(sb, indent + "    ", $"static (JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent + "    ", "{");
            sb.Append(keySb);
            L(sb, innerIndent, $"return {keyVar}.ValueKind == JsonValueKind.String ? {keyVar}.GetString() : null;");
            L(sb, indent + "    ", "},");
            L(sb, indent + "    ", $"static (JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent + "    ", "{");
            sb.Append(valSb);
            L(sb, innerIndent, $"return {valVar};");
            L(sb, indent + "    ", "},");
            L(sb, indent + "    ", $"{wsVar});");
            return v;
        }

        private string EmitComputedStep(
            StringBuilder sb, JsonataNode step, string currentVar, string indent, string wsVar)
        {
            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "    ";
            StringBuilder lambdaBody = new();

            string bodyResult = EmitExpression(lambdaBody, step, innerIndent, elParam, wsParam);

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.ApplyStepOverElements({currentVar}, static (JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent, "{");
            sb.Append(lambdaBody);
            L(sb, innerIndent, $"return {bodyResult};");
            L(sb, indent, $"}}, {wsVar});");
            return v;
        }

        // ── Binary ───────────────────────────────────────────
        private string EmitBinary(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar, string wsVar)
        {
            return binary.Operator switch
            {
                "+" => EmitArithmetic(sb, binary, indent, dataVar, wsVar, "Add"),
                "-" => EmitArithmetic(sb, binary, indent, dataVar, wsVar, "Subtract"),
                "*" => EmitArithmetic(sb, binary, indent, dataVar, wsVar, "Multiply"),
                "/" => EmitArithmetic(sb, binary, indent, dataVar, wsVar, "Divide"),
                "%" => EmitArithmetic(sb, binary, indent, dataVar, wsVar, "Modulo"),
                "=" => EmitComparison(sb, binary, indent, dataVar, wsVar, "AreEqual"),
                "!=" => EmitComparison(sb, binary, indent, dataVar, wsVar, "AreNotEqual"),
                "<" => EmitComparison(sb, binary, indent, dataVar, wsVar, "LessThan"),
                "<=" => EmitComparison(sb, binary, indent, dataVar, wsVar, "LessThanOrEqual"),
                ">" => EmitComparison(sb, binary, indent, dataVar, wsVar, "GreaterThan"),
                ">=" => EmitComparison(sb, binary, indent, dataVar, wsVar, "GreaterThanOrEqual"),
                "&" => EmitStringConcat(sb, binary, indent, dataVar, wsVar),
                "and" => EmitLogical(sb, binary, indent, dataVar, wsVar, isAnd: true),
                "or" => EmitLogical(sb, binary, indent, dataVar, wsVar, isAnd: false),
                "in" => EmitIn(sb, binary, indent, dataVar, wsVar),
                _ => throw new FallbackException(),
            };
        }

        private string EmitArithmetic(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar,
            string wsVar, string helperName)
        {
            // Constant folding
            if (binary.Lhs is NumberNode lNum && binary.Rhs is NumberNode rNum)
            {
                double result = helperName switch
                {
                    "Add" => lNum.Value + rNum.Value,
                    "Subtract" => lNum.Value - rNum.Value,
                    "Multiply" => lNum.Value * rNum.Value,
                    "Divide" => rNum.Value != 0 ? lNum.Value / rNum.Value : double.PositiveInfinity,
                    "Modulo" => rNum.Value != 0 ? lNum.Value % rNum.Value : double.NaN,
                    _ => throw new FallbackException(),
                };

                if (!double.IsNaN(result) && !double.IsInfinity(result))
                {
                    return EmitDoubleConstant(sb, result, indent, wsVar);
                }
            }

            string lhs = EmitExpression(sb, binary.Lhs, indent, dataVar, wsVar);
            string rhs = EmitExpression(sb, binary.Rhs, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({lhs}, {rhs}, {wsVar});");
            return v;
        }

        private string EmitComparison(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar,
            string wsVar, string helperName)
        {
            string lhs = EmitExpression(sb, binary.Lhs, indent, dataVar, wsVar);
            string rhs = EmitExpression(sb, binary.Rhs, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.BooleanElement({H}.{helperName}({lhs}, {rhs}));");
            return v;
        }

        private string EmitStringConcat(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar, string wsVar)
        {
            string lhs = EmitExpression(sb, binary.Lhs, indent, dataVar, wsVar);
            string rhs = EmitExpression(sb, binary.Rhs, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.StringConcat({lhs}, {rhs}, {wsVar});");
            return v;
        }

        private string EmitLogical(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar,
            string wsVar, bool isAnd)
        {
            string lhs = EmitExpression(sb, binary.Lhs, indent, dataVar, wsVar);
            string v = NextVar();

            if (isAnd)
            {
                L(sb, indent, $"JsonElement {v};");
                L(sb, indent, $"if (!{H}.IsTruthy({lhs}))");
                L(sb, indent, "{");
                L(sb, indent + "    ", $"{v} = {H}.False;");
                L(sb, indent, "}");
                L(sb, indent, "else");
                L(sb, indent, "{");

                string rhs = EmitExpression(sb, binary.Rhs, indent + "    ", dataVar, wsVar);
                L(sb, indent + "    ", $"{v} = {H}.BooleanElement({H}.IsTruthy({rhs}));");
                L(sb, indent, "}");
            }
            else
            {
                L(sb, indent, $"JsonElement {v};");
                L(sb, indent, $"if ({H}.IsTruthy({lhs}))");
                L(sb, indent, "{");
                L(sb, indent + "    ", $"{v} = {H}.True;");
                L(sb, indent, "}");
                L(sb, indent, "else");
                L(sb, indent, "{");

                string rhs = EmitExpression(sb, binary.Rhs, indent + "    ", dataVar, wsVar);
                L(sb, indent + "    ", $"{v} = {H}.BooleanElement({H}.IsTruthy({rhs}));");
                L(sb, indent, "}");
            }

            return v;
        }

        private string EmitIn(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar, string wsVar)
        {
            string lhs = EmitExpression(sb, binary.Lhs, indent, dataVar, wsVar);
            string rhs = EmitExpression(sb, binary.Rhs, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.BooleanElement({H}.In({lhs}, {rhs}));");
            return v;
        }

        // ── Unary ────────────────────────────────────────────
        private string EmitUnary(
            StringBuilder sb, UnaryNode unary, string indent, string dataVar, string wsVar)
        {
            if (unary.Operator != "-")
            {
                throw new FallbackException();
            }

            if (unary.Expression is NumberNode num)
            {
                return EmitDoubleConstant(sb, -num.Value, indent, wsVar);
            }

            string operand = EmitExpression(sb, unary.Expression, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.Negate({operand}, {wsVar});");
            return v;
        }

        // ── Literals ─────────────────────────────────────────
        private string EmitNumber(StringBuilder sb, NumberNode num, string indent, string wsVar)
        {
            return EmitDoubleConstant(sb, num.Value, indent, wsVar);
        }

        private string EmitDoubleConstant(StringBuilder sb, double value, string indent, string wsVar)
        {
            string v = NextVar();
            if (value == 0.0)
            {
                L(sb, indent, $"var {v} = {H}.Zero;");
            }
            else if (value == 1.0)
            {
                L(sb, indent, $"var {v} = {H}.One;");
            }
            else
            {
                string literal = value.ToString("R", CultureInfo.InvariantCulture);
                L(sb, indent, $"var {v} = {H}.NumberFromDouble({literal}, {wsVar});");
            }

            return v;
        }

        private string EmitString(StringBuilder sb, StringNode str, string indent, string wsVar)
        {
            string v = NextVar();
            if (str.Value.Length == 0)
            {
                L(sb, indent, $"var {v} = {H}.EmptyString;");
            }
            else
            {
                string escaped = EscapeStringLiteral(str.Value);
                L(sb, indent, $"var {v} = {H}.StringElement(\"{escaped}\", {wsVar});");
            }

            return v;
        }

        private string EmitValue(StringBuilder sb, ValueNode val, string indent)
        {
            string v = NextVar();
            string helper = val.Value switch
            {
                "true" => "True",
                "false" => "False",
                "null" => "Null",
                _ => throw new FallbackException(),
            };

            L(sb, indent, $"var {v} = {H}.{helper};");
            return v;
        }

        // ── Name / Variable ──────────────────────────────────
        private string EmitName(
            StringBuilder sb, NameNode name, string indent, string dataVar, string wsVar)
        {
            string nameField = GetOrCreateNameField(name.Value);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.NavigateProperty({dataVar}, {nameField}, {wsVar});");
            return v;
        }

        private string EmitVariable(
            StringBuilder sb, VariableNode variable, string indent, string dataVar)
        {
            // $ (empty name) -> root data context
            if (string.IsNullOrEmpty(variable.Name))
            {
                return dataVar;
            }

            if (_variables.TryGetValue(variable.Name, out string? csVar))
            {
                return csVar;
            }

            // Undefined variable
            string v = NextVar();
            L(sb, indent, $"var {v} = default(JsonElement);");
            return v;
        }

        // ── Condition ────────────────────────────────────────
        private string EmitCondition(
            StringBuilder sb, ConditionNode cond, string indent, string dataVar, string wsVar)
        {
            string condVar = EmitExpression(sb, cond.Condition, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"JsonElement {v};");
            L(sb, indent, $"if ({H}.IsTruthy({condVar}))");
            L(sb, indent, "{");

            string thenVar = EmitExpression(sb, cond.Then, indent + "    ", dataVar, wsVar);
            L(sb, indent + "    ", $"{v} = {thenVar};");
            L(sb, indent, "}");

            if (cond.Else is not null)
            {
                L(sb, indent, "else");
                L(sb, indent, "{");

                string elseVar = EmitExpression(sb, cond.Else, indent + "    ", dataVar, wsVar);
                L(sb, indent + "    ", $"{v} = {elseVar};");
                L(sb, indent, "}");
            }
            else
            {
                L(sb, indent, "else");
                L(sb, indent, "{");
                L(sb, indent + "    ", $"{v} = default;");
                L(sb, indent, "}");
            }

            return v;
        }

        // ── Block / Bind ─────────────────────────────────────
        private string EmitBlock(
            StringBuilder sb, BlockNode block, string indent, string dataVar, string wsVar)
        {
            string lastVar = dataVar;
            foreach (JsonataNode expr in block.Expressions)
            {
                lastVar = EmitExpression(sb, expr, indent, dataVar, wsVar);
            }

            return lastVar;
        }

        private string EmitBind(
            StringBuilder sb, BindNode bind, string indent, string dataVar, string wsVar)
        {
            if (bind.Lhs is not VariableNode varNode)
            {
                throw new FallbackException();
            }

            string rhsVar = EmitExpression(sb, bind.Rhs, indent, dataVar, wsVar);
            _variables[varNode.Name] = rhsVar;
            return rhsVar;
        }

        // ── Array / Object constructors ──────────────────────
        private string EmitArrayConstructor(
            StringBuilder sb, ArrayConstructorNode arr, string indent, string dataVar, string wsVar)
        {
            if (arr.Expressions.Count == 0)
            {
                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.CreateArray(System.Array.Empty<JsonElement>(), {wsVar});");
                return v;
            }

            string[] elemVars = new string[arr.Expressions.Count];
            for (int i = 0; i < arr.Expressions.Count; i++)
            {
                elemVars[i] = EmitExpression(sb, arr.Expressions[i], indent, dataVar, wsVar);
            }

            string v2 = NextVar();
            L(sb, indent, $"var {v2} = {H}.CreateArray(new JsonElement[] {{ {string.Join(", ", elemVars)} }}, {wsVar});");
            return v2;
        }

        private string EmitObjectConstructor(
            StringBuilder sb, ObjectConstructorNode obj, string indent, string dataVar, string wsVar)
        {
            foreach ((JsonataNode key, _) in obj.Pairs)
            {
                if (key is not StringNode)
                {
                    throw new FallbackException();
                }
            }

            string[] keyStrings = new string[obj.Pairs.Count];
            string[] valueVars = new string[obj.Pairs.Count];

            for (int i = 0; i < obj.Pairs.Count; i++)
            {
                keyStrings[i] = $"\"{EscapeStringLiteral(((StringNode)obj.Pairs[i].Key).Value)}\"";
                valueVars[i] = EmitExpression(sb, obj.Pairs[i].Value, indent, dataVar, wsVar);
            }

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.CreateObject(");
            L(sb, indent + "    ", $"new string[] {{ {string.Join(", ", keyStrings)} }},");
            L(sb, indent + "    ", $"new JsonElement[] {{ {string.Join(", ", valueVars)} }},");
            L(sb, indent + "    ", $"{wsVar});");
            return v;
        }

        // ── Function call ────────────────────────────────────
        private string EmitFunctionCall(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar, string wsVar)
        {
            if (func.Procedure is not VariableNode procVar)
            {
                throw new FallbackException();
            }

            return procVar.Name switch
            {
                "sum" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Sum"),
                "count" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Count"),
                "string" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "String"),
                "length" => throw new FallbackException(),
                "boolean" => EmitBuiltinBoolean(sb, func, indent, dataVar, wsVar, negate: false),
                "not" => EmitBuiltinBoolean(sb, func, indent, dataVar, wsVar, negate: true),
                "join" => EmitBuiltinJoin(sb, func, indent, dataVar, wsVar),
                "map" => EmitHof(sb, func, indent, dataVar, wsVar, "MapElements", returnsElement: true),
                "filter" => EmitHof(sb, func, indent, dataVar, wsVar, "FilterElements", returnsElement: false),
                "reduce" => EmitHofReduce(sb, func, indent, dataVar, wsVar),
                "sort" => EmitHofSort(sb, func, indent, dataVar, wsVar),
                _ => throw new FallbackException(),
            };
        }

        private string EmitBuiltinUnary(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName)
        {
            if (func.Arguments.Count < 1)
            {
                throw new FallbackException();
            }

            string arg = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({arg}, {wsVar});");
            return v;
        }

        private string EmitBuiltinBoolean(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, bool negate)
        {
            if (func.Arguments.Count < 1)
            {
                throw new FallbackException();
            }

            string arg = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string v = NextVar();
            string neg = negate ? "!" : string.Empty;
            L(sb, indent, $"var {v} = {H}.BooleanElement({neg}{H}.IsTruthy({arg}));");
            return v;
        }

        private string EmitBuiltinJoin(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar, string wsVar)
        {
            if (func.Arguments.Count < 1)
            {
                throw new FallbackException();
            }

            string arg0 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string arg1 = func.Arguments.Count >= 2
                ? EmitExpression(sb, func.Arguments[1], indent, dataVar, wsVar)
                : "default";

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.Join({arg0}, {arg1}, {wsVar});");
            return v;
        }

        // ── HOF: $map / $filter ──────────────────────────────
        private string EmitHof(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName, bool returnsElement)
        {
            if (func.Arguments.Count < 2 || func.Arguments[1] is not LambdaNode lambda)
            {
                throw new FallbackException();
            }

            if (lambda.Parameters.Count < 1)
            {
                throw new FallbackException();
            }

            string inputVar = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);

            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "    ";
            StringBuilder lambdaBody = new();

            string? savedVar = StashVariable(lambda.Parameters[0], elParam);
            string bodyResult = EmitExpression(lambdaBody, lambda.Body, innerIndent, elParam, wsParam);
            RestoreVariable(lambda.Parameters[0], savedVar);

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({inputVar}, static (JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent, "{");
            sb.Append(lambdaBody);

            if (returnsElement)
            {
                L(sb, innerIndent, $"return {bodyResult};");
            }
            else
            {
                L(sb, innerIndent, $"return {H}.IsTruthy({bodyResult});");
            }

            L(sb, indent, $"}}, {wsVar});");
            return v;
        }

        // ── HOF: $reduce ─────────────────────────────────────
        private string EmitHofReduce(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar, string wsVar)
        {
            if (func.Arguments.Count < 2 || func.Arguments[1] is not LambdaNode lambda)
            {
                throw new FallbackException();
            }

            if (lambda.Parameters.Count < 2)
            {
                throw new FallbackException();
            }

            string inputVar = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string initVar = func.Arguments.Count >= 3
                ? EmitExpression(sb, func.Arguments[2], indent, dataVar, wsVar)
                : "default";

            int lambdaIdx = _lambdaCounter++;
            string prevParam = $"prev_{lambdaIdx}";
            string currParam = $"curr_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "    ";
            StringBuilder lambdaBody = new();

            string? savedPrev = StashVariable(lambda.Parameters[0], prevParam);
            string? savedCurr = StashVariable(lambda.Parameters[1], currParam);
            string bodyResult = EmitExpression(lambdaBody, lambda.Body, innerIndent, currParam, wsParam);
            RestoreVariable(lambda.Parameters[1], savedCurr);
            RestoreVariable(lambda.Parameters[0], savedPrev);

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.ReduceElements({inputVar}, {initVar},");
            L(sb, indent + "    ", $"static (JsonElement {prevParam}, JsonElement {currParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent + "    ", "{");
            sb.Append(lambdaBody);
            L(sb, innerIndent, $"return {bodyResult};");
            L(sb, indent + "    ", "},");
            L(sb, indent + "    ", $"{wsVar});");
            return v;
        }

        // ── HOF: $sort ───────────────────────────────────────
        private string EmitHofSort(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar, string wsVar)
        {
            if (func.Arguments.Count < 2 || func.Arguments[1] is not LambdaNode lambda)
            {
                throw new FallbackException();
            }

            if (lambda.Parameters.Count < 2)
            {
                throw new FallbackException();
            }

            string inputVar = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);

            int lambdaIdx = _lambdaCounter++;
            string aParam = $"a_{lambdaIdx}";
            string bParam = $"b_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "    ";
            StringBuilder lambdaBody = new();

            string? savedA = StashVariable(lambda.Parameters[0], aParam);
            string? savedB = StashVariable(lambda.Parameters[1], bParam);
            string bodyResult = EmitExpression(lambdaBody, lambda.Body, innerIndent, aParam, wsParam);
            RestoreVariable(lambda.Parameters[1], savedB);
            RestoreVariable(lambda.Parameters[0], savedA);

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.Sort({inputVar},");
            L(sb, indent + "    ", $"static (JsonElement {aParam}, JsonElement {bParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent + "    ", "{");
            sb.Append(lambdaBody);
            L(sb, innerIndent, $"return {H}.IsTruthy({bodyResult});");
            L(sb, indent + "    ", "},");
            L(sb, indent + "    ", $"{wsVar});");
            return v;
        }

        // ── Filter (standalone) ──────────────────────────────
        private string EmitFilter(
            StringBuilder sb, FilterNode filter, string indent, string dataVar, string wsVar)
        {
            string predVar = EmitExpression(sb, filter.Expression, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.IsTruthy({predVar}) ? {dataVar} : default(JsonElement);");
            return v;
        }

        // ── Variable scoping helpers ─────────────────────────
        private string? StashVariable(string name, string csVar)
        {
            _variables.TryGetValue(name, out string? old);
            _variables[name] = csVar;
            return old;
        }

        private void RestoreVariable(string name, string? old)
        {
            if (old is not null)
            {
                _variables[name] = old;
            }
            else
            {
                _variables.Remove(name);
            }
        }
    }
}