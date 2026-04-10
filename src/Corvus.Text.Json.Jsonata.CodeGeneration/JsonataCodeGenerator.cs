// <copyright file="JsonataCodeGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
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

        // Copy data to a local so it can be captured in lambdas (in parameters cannot be captured).
        L(sb, "        ", "var __rootData = data;");
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

        /// <summary>
        /// When true, the expression references <c>$$</c> (root data). Lambdas cannot
        /// be <c>static</c> because they need to capture <c>__rootData</c>.
        /// </summary>
        private bool _usesRootRef;

        /// <summary>
        /// Returns <c>"static "</c> when lambdas can be static, or <c>""</c> when
        /// they need to capture <c>__rootData</c>.
        /// </summary>
        private string Static => _usesRootRef ? "" : "static ";

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
            string result = node switch
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
                FilterNode => throw new FallbackException(),
                _ => throw new FallbackException(),
            };

            // Apply stages on standalone expression nodes (e.g. $[0], name[0])
            // PathNode handles its own stages internally, so skip it.
            if (node is not PathNode && HasStages(node))
            {
                if (HasComplexAnnotations(node))
                {
                    throw new FallbackException();
                }

                foreach (JsonataNode stage in node.Annotations!.Stages)
                {
                    result = EmitFilterStage(sb, stage, result, indent, wsVar);
                }
            }

            // Apply group-by on standalone expression nodes (e.g. $${key: value}).
            // PathNode handles its own group-by internally, so skip it.
            if (node is not PathNode && node.Annotations?.Group is not null)
            {
                result = EmitGroupByAnnotation(sb, node.Annotations.Group, result, indent, wsVar);
            }

            // Handle KeepArray (the [] suffix): ensure result is always an array
            if (node.KeepArray)
            {
                string wrapped = NextVar();
                L(sb, indent, $"var {wrapped} = {H}.WrapAsArray({result}, {wsVar});");
                result = wrapped;
            }

            return result;
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

            // Check for simple property chain (all NameNode, no annotations on steps or path)
            if (IsSimplePropertyChain(steps) && path.Annotations?.Group is null)
            {
                string result = EmitSimplePropertyChain(sb, steps, indent, dataVar, wsVar);

                // Still apply KeepSingletonArray wrapping if needed (e.g. number[])
                if (path.KeepSingletonArray || path.KeepArray
                    || steps.Exists(s => s.KeepArray))
                {
                    string wrapped = NextVar();
                    L(sb, indent, $"var {wrapped} = {H}.KeepSingletonArray({result}, {wsVar});");
                    result = wrapped;
                }

                return result;
            }

            // Process step by step
            string currentVar = dataVar;
            int i = 0;

            // Handle leading VariableNode (including stages like $[0])
            if (steps[0] is VariableNode leadVar)
            {
                if (HasComplexAnnotations(leadVar))
                {
                    throw new FallbackException();
                }

                currentVar = EmitVariable(sb, leadVar, indent, dataVar);

                // Apply stages on the variable (e.g. $[0] applies index to root)
                if (HasStages(leadVar))
                {
                    foreach (JsonataNode stage in leadVar.Annotations!.Stages)
                    {
                        currentVar = EmitFilterStage(sb, stage, currentVar, indent, wsVar);
                    }
                }

                // Apply group-by on the variable (e.g. $${key: value})
                if (leadVar.Annotations?.Group is not null)
                {
                    currentVar = EmitGroupByAnnotation(sb, leadVar.Annotations.Group, currentVar, indent, wsVar);
                }

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
                           && !HasComplexAnnotations(steps[i])
                           && !HasGroupAnnotation(steps[i]))
                    {
                        i++;
                    }

                    if (i > segStart)
                    {
                        currentVar = EmitPropertyChainSegment(sb, steps, segStart, i, indent, currentVar, wsVar);
                    }

                    // NameNode with unsupported annotations (parent %, join @, index #)
                    // that have no stages or group-by — fall back to runtime.
                    // Without this check, i is never incremented and the loop hangs.
                    if (i < steps.Count && steps[i] is NameNode
                        && HasComplexAnnotations(steps[i])
                        && !HasStages(steps[i])
                        && !HasGroupAnnotation(steps[i]))
                    {
                        throw new FallbackException();
                    }

                    // If the next step is a NameNode with filter stages or group-by, handle it.
                    // At step 0 (first in path), stages are applied GLOBALLY after navigation.
                    // At step > 0, stages are applied PER-ELEMENT before aggregation.
                    // This matches the runtime's behaviour in CompilePath.
                    if (i < steps.Count && steps[i] is NameNode annotated
                        && (HasStages(annotated) || HasGroupAnnotation(annotated)))
                    {
                        if (HasComplexAnnotations(annotated))
                        {
                            throw new FallbackException();
                        }

                        if (HasStages(annotated))
                        {
                            if (i == 0)
                            {
                                // First step: navigate property, then apply stages globally
                                string nameField = GetOrCreateNameField(annotated.Value);
                                string navVar = NextVar();
                                L(sb, indent, $"var {navVar} = {H}.NavigateProperty({currentVar}, {nameField}, {wsVar});");
                                currentVar = navVar;

                                foreach (JsonataNode stage in annotated.Annotations!.Stages)
                                {
                                    currentVar = EmitFilterStage(sb, stage, currentVar, indent, wsVar);
                                }
                            }
                            else
                            {
                                // Subsequent step: per-element navigation + stages
                                currentVar = EmitAnnotatedNameStep(sb, annotated, currentVar, indent, wsVar);
                            }
                        }
                        else
                        {
                            // Group-by without stages: navigate property first
                            string nameField = GetOrCreateNameField(annotated.Value);
                            string navVar = NextVar();
                            L(sb, indent, $"var {navVar} = {H}.NavigateProperty({currentVar}, {nameField}, {wsVar});");
                            currentVar = navVar;
                        }

                        // Apply group-by if present (operates on the collected result)
                        if (annotated.Annotations?.Group is not null)
                        {
                            currentVar = EmitGroupByAnnotation(sb, annotated.Annotations.Group, currentVar, indent, wsVar);
                        }

                        i++;
                    }
                }
                else if (step is ObjectConstructorNode objCtor)
                {
                    if (i == 0)
                    {
                        // First step: literal object construction, then navigate
                        currentVar = EmitObjectConstructor(sb, objCtor, indent, dataVar, wsVar);
                    }
                    else
                    {
                        // Subsequent step: group-by (creates objects per element)
                        currentVar = EmitGroupByStep(sb, objCtor, currentVar, indent, wsVar);
                    }

                    i++;
                }
                else if (step is ArrayConstructorNode { ConsArray: true, Expressions.Count: 0 })
                {
                    // The `[]` step in a path serves as a KeepSingletonArray marker.
                    // When the result was produced by per-element mapping (normal path
                    // navigation/computed steps), auto-flattening already happened during
                    // collection, so we just advance. KeepSingletonArray at the end of
                    // EmitPath ensures the result is always an array.
                    // Note: genuine flatten for array literals like [1,[2,3]][] is handled
                    // by path step collection auto-flattening, not by an explicit FlattenArray.
                    i++;
                }
                else if (step is WildcardNode or DescendantNode)
                {
                    if (HasComplexAnnotations(step) || HasStages(step))
                    {
                        throw new FallbackException();
                    }

                    string helperName = step is WildcardNode ? "EnumerateWildcard" : "EnumerateDescendant";

                    if (i == 0)
                    {
                        string v = NextVar();
                        L(sb, indent, $"var {v} = {H}.{helperName}({currentVar}, {wsVar});");
                        currentVar = v;
                    }
                    else
                    {
                        // Map wildcard/descendant over each element with auto-flattening
                        string v = NextVar();
                        L(sb, indent, $"var {v} = {H}.ApplyStepOverElements({currentVar}, static (el, ws) => {H}.{helperName}(el, ws), {wsVar});");
                        currentVar = v;
                    }

                    i++;
                }
                else
                {
                    if (HasComplexAnnotations(step))
                    {
                        throw new FallbackException();
                    }

                    if (i == 0)
                    {
                        // First step: evaluate as direct expression (creates data source)
                        // EmitExpression handles stages internally, so no need to apply them here.
                        currentVar = EmitExpression(sb, step, indent, dataVar, wsVar);
                    }
                    else
                    {
                        // Subsequent step: map over elements
                        // EmitComputedStep calls EmitExpression inside its lambda,
                        // which applies stages per-element.
                        // When followed by [] (ConsArray), use collection semantics to always wrap.
                        bool nextIsConsArray = (i + 1 < steps.Count)
                            && steps[i + 1] is ArrayConstructorNode { ConsArray: true, Expressions.Count: 0 };
                        currentVar = EmitComputedStep(sb, step, currentVar, indent, wsVar, followedByConsArray: nextIsConsArray);
                    }

                    i++;
                }
            }

            // Handle group-by annotation on the PathNode itself (e.g. Phone{type: number}).
            // Group-by on individual steps is handled inside the step loop via EmitGroupByAnnotation.
            if (path.Annotations?.Group is not null)
            {
                currentVar = EmitGroupByAnnotation(sb, path.Annotations.Group, currentVar, indent, wsVar);
            }

            // Handle KeepSingletonArray: if any step has KeepArray or path has KeepArray, wrap singleton results
            bool keepSingleton = path.KeepSingletonArray || path.KeepArray;
            if (!keepSingleton)
            {
                for (int k = 0; k < steps.Count; k++)
                {
                    if (steps[k].KeepArray)
                    {
                        keepSingleton = true;
                        break;
                    }
                }
            }

            if (keepSingleton)
            {
                string wrapped = NextVar();
                L(sb, indent, $"var {wrapped} = {H}.KeepSingletonArray({currentVar}, {wsVar});");
                currentVar = wrapped;
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

                if (HasStages(step) || HasComplexAnnotations(step) || HasGroupAnnotation(step))
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

        private static bool HasGroupAnnotation(JsonataNode node)
        {
            return node.Annotations?.Group is not null;
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

            // General stage: evaluate expression and dispatch (numeric → index, other → filter)
            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "    ";
            StringBuilder lambdaBody = new();

            string stageResultVar = EmitExpression(lambdaBody, filter.Expression, innerIndent, elParam, wsParam);

            string v2 = NextVar();
            L(sb, indent, $"var {v2} = {H}.ApplyStage({currentVar}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent, "{");
            sb.Append(lambdaBody);
            L(sb, innerIndent, $"return {stageResultVar};");
            L(sb, indent, $"}}, {wsVar});");
            return v2;
        }

        /// <summary>
        /// Emits a name step with stages using per-element evaluation.
        /// Wraps navigation + filter stages inside <c>ApplyStepOverElements</c>
        /// so that stages are applied to each individual navigation result before
        /// aggregation, matching the runtime's per-element stage semantics.
        /// </summary>
        private string EmitAnnotatedNameStep(
            StringBuilder sb, NameNode annotated, string currentVar, string indent, string wsVar)
        {
            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "    ";
            StringBuilder innerBody = new();

            // Navigate the property inside the per-element lambda
            string nameField = GetOrCreateNameField(annotated.Value);
            string navVar = NextVar();
            L(innerBody, innerIndent, $"var {navVar} = {H}.NavigateProperty({elParam}, {nameField}, {wsParam});");
            string stageResult = navVar;

            // Apply filter stages per-element
            foreach (JsonataNode stage in annotated.Annotations!.Stages)
            {
                stageResult = EmitPerElementFilterStage(innerBody, stage, stageResult, innerIndent, wsParam);
            }

            string resultVar = NextVar();
            L(sb, indent, $"var {resultVar} = {H}.ApplyStepOverElements({currentVar}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent, "{");
            sb.Append(innerBody);
            L(sb, innerIndent, $"return {stageResult};");
            L(sb, indent, $"}}, {wsVar});");
            return resultVar;
        }

        /// <summary>
        /// Emits a filter stage for per-element evaluation within a path step.
        /// Uses <c>ArrayIndexPerElement</c> for numeric indices (handles singleton-at-0).
        /// </summary>
        private string EmitPerElementFilterStage(
            StringBuilder sb, JsonataNode stage, string currentVar, string indent, string wsVar)
        {
            if (stage is not FilterNode filter)
            {
                throw new FallbackException();
            }

            // Numeric index -> ArrayIndexPerElement (per-element semantics)
            if (filter.Expression is NumberNode numIdx)
            {
                int idx = (int)numIdx.Value;
                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.ArrayIndexPerElement({currentVar}, {idx});");
                return v;
            }

            // General stage: evaluate and dispatch (numeric → index, other → filter)
            int innerLambdaIdx = _lambdaCounter++;
            string elParam = $"el_{innerLambdaIdx}";
            string wsParam = $"ws_{innerLambdaIdx}";
            string innerIndent = indent + "    ";
            StringBuilder lambdaBody = new();

            string stageResultVar = EmitExpression(lambdaBody, filter.Expression, innerIndent, elParam, wsParam);

            string v2 = NextVar();
            L(sb, indent, $"var {v2} = {H}.ApplyStage({currentVar}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent, "{");
            sb.Append(lambdaBody);
            L(sb, innerIndent, $"return {stageResultVar};");
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

            // Path-step group-by (.{key: value}): apply per-element.
            // Each element gets its own GroupByObject call producing a single-entry object.
            // GroupByObjectPerElement collects these into an array with singleton semantics.
            // KeepArray (the [] modifier) is handled by the value expression itself,
            // matching the runtime where WrapKeepArray is part of the compiled evaluator.
            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "        ";

            StringBuilder keySb = new();
            string keyVar = EmitExpression(keySb, keyExpr, innerIndent, elParam, wsParam);

            StringBuilder valSb = new();
            string valVar = EmitExpression(valSb, valExpr, innerIndent, elParam, wsParam);

            string v = NextVar();

            L(sb, indent, $"var {v} = {H}.GroupByObjectPerElement({currentVar},");
            L(sb, indent + "    ", $"{Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent + "    ", "{");
            sb.Append(keySb);
            L(sb, innerIndent, $"return {H}.ValidateGroupByKey({keyVar});");
            L(sb, indent + "    ", "},");
            L(sb, indent + "    ", $"{Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
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

            // Annotation group-by (expr{key: value}): two-phase approach matching the runtime.
            // Phase 1: Group ELEMENTS by key.
            // Phase 2: For each group, build context (single element or array), evaluate VALUE.
            // KeepArray (the [] modifier) is handled by the value expression itself —
            // the runtime's Compile() wraps the value evaluator with WrapKeepArray.
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
            L(sb, indent + "    ", $"{Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent + "    ", "{");
            sb.Append(keySb);
            L(sb, innerIndent, $"return {H}.ValidateGroupByKey({keyVar});");
            L(sb, indent + "    ", "},");
            L(sb, indent + "    ", $"{Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent + "    ", "{");
            sb.Append(valSb);
            L(sb, innerIndent, $"return {valVar};");
            L(sb, indent + "    ", "},");
            L(sb, indent + "    ", $"{wsVar});");
            return v;
        }

        private string EmitComputedStep(
            StringBuilder sb, JsonataNode step, string currentVar, string indent, string wsVar,
            bool followedByConsArray = false)
        {
            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "    ";
            StringBuilder lambdaBody = new();

            string bodyResult = EmitExpression(lambdaBody, step, innerIndent, elParam, wsParam);

            // Array constructor steps (e.g. .[expr,expr]) use NoFlatten to preserve array structure.
            // When followed by [] (either as a separate ConsArray step or absorbed as KeepArray on the node),
            // use ApplyStepCollectingResults to always produce a collection array (wrapping even single-element results).
            bool needsCollectionSemantics = followedByConsArray || step.KeepArray;
            string helperName = step is ArrayConstructorNode
                ? (needsCollectionSemantics ? "ApplyStepCollectingResults" : "ApplyStepOverElementsNoFlatten")
                : "ApplyStepOverElements";

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({currentVar}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
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
                "=" => EmitEqualityComparison(sb, binary, indent, dataVar, wsVar, "AreEqual"),
                "!=" => EmitEqualityComparison(sb, binary, indent, dataVar, wsVar, "AreNotEqual"),
                "<" => EmitOrderedComparison(sb, binary, indent, dataVar, wsVar, "LessThan"),
                "<=" => EmitOrderedComparison(sb, binary, indent, dataVar, wsVar, "LessThanOrEqual"),
                ">" => EmitOrderedComparison(sb, binary, indent, dataVar, wsVar, "GreaterThan"),
                ">=" => EmitOrderedComparison(sb, binary, indent, dataVar, wsVar, "GreaterThanOrEqual"),
                "&" => EmitStringConcat(sb, binary, indent, dataVar, wsVar),
                "and" => EmitLogical(sb, binary, indent, dataVar, wsVar, isAnd: true),
                "or" => EmitLogical(sb, binary, indent, dataVar, wsVar, isAnd: false),
                "in" => EmitIn(sb, binary, indent, dataVar, wsVar),
                ".." => EmitRange(sb, binary, indent, dataVar, wsVar),
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

                // Non-finite constant result (e.g. 1/0 → Infinity) — the runtime evaluator
                // handles these differently (may allow Infinity to propagate to $string which
                // throws D3001). Fall back so the runtime produces the correct error.
                throw new FallbackException();
            }

            string lhs = EmitExpression(sb, binary.Lhs, indent, dataVar, wsVar);
            string rhs = EmitExpression(sb, binary.Rhs, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({lhs}, {rhs}, {wsVar});");
            return v;
        }

        private string EmitEqualityComparison(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar,
            string wsVar, string helperName)
        {
            string lhs = EmitExpression(sb, binary.Lhs, indent, dataVar, wsVar);
            string rhs = EmitExpression(sb, binary.Rhs, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.BooleanElement({H}.{helperName}({lhs}, {rhs}));");
            return v;
        }

        /// <summary>
        /// Emits ordered comparison (&lt;, &lt;=, &gt;, &gt;=).
        /// These return <c>JsonElement</c> directly (may be <c>default</c>/undefined
        /// when one operand is undefined).
        /// </summary>
        private string EmitOrderedComparison(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar,
            string wsVar, string helperName)
        {
            string lhs = EmitExpression(sb, binary.Lhs, indent, dataVar, wsVar);
            string rhs = EmitExpression(sb, binary.Rhs, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({lhs}, {rhs});");
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

        private string EmitRange(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar, string wsVar)
        {
            // Constant-fold when both sides are integer literals
            if (binary.Lhs is NumberNode lNum && binary.Rhs is NumberNode rNum)
            {
                double start = lNum.Value;
                double end = rNum.Value;
                if (start == Math.Floor(start) && end == Math.Floor(end) && start <= end)
                {
                    long count = (long)end - (long)start + 1;
                    if (count <= 100)
                    {
                        // Small constant ranges: emit inline array literal
                        var elVars = new string[(int)count];
                        for (int i = 0; i < (int)count; i++)
                        {
                            elVars[i] = EmitDoubleConstant(sb, start + i, indent, wsVar);
                        }

                        string arrV = NextVar();
                        L(sb, indent, $"var {arrV} = {H}.CreateArray(new JsonElement[] {{ {string.Join(", ", elVars)} }}, {wsVar});");
                        return arrV;
                    }
                }
            }

            string lhs = EmitExpression(sb, binary.Lhs, indent, dataVar, wsVar);
            string rhs = EmitExpression(sb, binary.Rhs, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.Range({lhs}, {rhs}, {wsVar});");
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
            // $ (empty name) -> current data context
            if (string.IsNullOrEmpty(variable.Name))
            {
                return dataVar;
            }

            // $$ -> always the original root input (via local copy to allow lambda capture)
            if (variable.Name == "$")
            {
                _usesRootRef = true;
                return "__rootData";
            }

            if (_variables.TryGetValue(variable.Name, out string? csVar))
            {
                return csVar;
            }

            // Built-in function names used as values (e.g. $string($boolean))
            // cannot be represented in codegen — fall back to runtime.
            if (IsBuiltinFunctionName(variable.Name))
            {
                throw new FallbackException();
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
            if (block.Expressions.Count == 0)
            {
                // Empty block () → undefined
                string v = NextVar();
                L(sb, indent, $"var {v} = default(JsonElement);");
                return v;
            }

            // Track variables bound in this block so we can restore outer scope
            var boundVars = new List<(string name, string? saved)>();
            string lastVar = dataVar;
            foreach (JsonataNode expr in block.Expressions)
            {
                if (expr is BindNode bind && bind.Lhs is VariableNode varNode)
                {
                    string rhsVar = EmitExpression(sb, bind.Rhs, indent, dataVar, wsVar);
                    string? saved = StashVariable(varNode.Name, rhsVar);
                    boundVars.Add((varNode.Name, saved));
                    lastVar = rhsVar;
                }
                else
                {
                    lastVar = EmitExpression(sb, expr, indent, dataVar, wsVar);
                }
            }

            // Restore outer scope for all variables bound in this block
            for (int i = boundVars.Count - 1; i >= 0; i--)
            {
                RestoreVariable(boundVars[i].name, boundVars[i].saved);
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
            long isArrayCtorMask = 0;
            for (int i = 0; i < arr.Expressions.Count; i++)
            {
                elemVars[i] = EmitExpression(sb, arr.Expressions[i], indent, dataVar, wsVar);
                if (arr.Expressions[i] is ArrayConstructorNode)
                {
                    isArrayCtorMask |= 1L << i;
                }
            }

            string v2 = NextVar();

            // If any expression is NOT an array constructor, we need flatten semantics
            bool needsFlatten = isArrayCtorMask != 0 && isArrayCtorMask != ((1L << arr.Expressions.Count) - 1);

            // Also need flatten when no array constructors but some expressions might produce arrays
            // In JSONata, [path.to.array] flattens the array result
            if (isArrayCtorMask == 0)
            {
                // All expressions are non-array-constructor: all array results should be flattened
                needsFlatten = true;
            }

            if (needsFlatten)
            {
                L(sb, indent, $"var {v2} = {H}.CreateArrayWithFlatten(new JsonElement[] {{ {string.Join(", ", elemVars)} }}, {isArrayCtorMask}L, {wsVar});");
            }
            else
            {
                // All expressions are array constructors — no flattening needed
                L(sb, indent, $"var {v2} = {H}.CreateArray(new JsonElement[] {{ {string.Join(", ", elemVars)} }}, {wsVar});");
            }

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
                // Existing functions
                "sum" => EmitBuiltinUnaryOrError(sb, func, indent, dataVar, wsVar, "Sum", "sum"),
                "count" => EmitBuiltinUnaryOrError(sb, func, indent, dataVar, wsVar, "Count", "count"),
                "string" => EmitBuiltinString(sb, func, indent, dataVar, wsVar),
                "boolean" => EmitBuiltinBoolean(sb, func, indent, dataVar, wsVar, negate: false),
                "not" => EmitBuiltinBoolean(sb, func, indent, dataVar, wsVar, negate: true),
                "join" => EmitBuiltinJoin(sb, func, indent, dataVar, wsVar),
                "map" => EmitHof(sb, func, indent, dataVar, wsVar, "MapElements", returnsElement: true),
                "filter" => EmitHof(sb, func, indent, dataVar, wsVar, "FilterElements", returnsElement: false),
                "reduce" => EmitHofReduce(sb, func, indent, dataVar, wsVar),
                "sort" => EmitHofSort(sb, func, indent, dataVar, wsVar),

                // Phase 1a: Simple functions
                "exists" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Exists"),
                "type" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Type"),
                "length" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Length"),
                "number" => EmitBuiltinContextOptional(sb, func, indent, dataVar, wsVar, "Number"),
                "max" => EmitBuiltinUnaryOrError(sb, func, indent, dataVar, wsVar, "Max", "max"),
                "min" => EmitBuiltinUnaryOrError(sb, func, indent, dataVar, wsVar, "Min", "min"),
                "average" => EmitBuiltinUnaryOrError(sb, func, indent, dataVar, wsVar, "Average", "average"),

                // Phase 1b: Math functions
                "abs" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Abs"),
                "floor" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Floor"),
                "ceil" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Ceil"),
                "sqrt" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Sqrt"),
                "round" => EmitBuiltinOptionalSecond(sb, func, indent, dataVar, wsVar, "Round"),
                "power" => EmitBuiltinBinary(sb, func, indent, dataVar, wsVar, "Power"),

                // Phase 1c: String transforms
                "uppercase" => EmitBuiltinContextOptional(sb, func, indent, dataVar, wsVar, "Uppercase"),
                "lowercase" => EmitBuiltinContextOptional(sb, func, indent, dataVar, wsVar, "Lowercase"),
                "trim" => EmitBuiltinContextOptional(sb, func, indent, dataVar, wsVar, "Trim"),
                "substring" => EmitBuiltinOptionalThird(sb, func, indent, dataVar, wsVar, "Substring"),
                "substringBefore" => EmitBuiltinBinary(sb, func, indent, dataVar, wsVar, "SubstringBefore"),
                "substringAfter" => EmitBuiltinBinary(sb, func, indent, dataVar, wsVar, "SubstringAfter"),
                "contains" => EmitBuiltinContextImpliedBinary(sb, func, indent, dataVar, wsVar, "Contains"),
                "split" => EmitBuiltinSplit(sb, func, indent, dataVar, wsVar),
                "pad" => EmitBuiltinOptionalThird(sb, func, indent, dataVar, wsVar, "Pad"),

                // Phase 1d: Array/Object ops
                "append" => EmitBuiltinBinary(sb, func, indent, dataVar, wsVar, "Append"),
                "reverse" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Reverse"),
                "distinct" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Distinct"),
                "keys" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Keys"),
                "values" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Values"),
                "lookup" => EmitBuiltinBinary(sb, func, indent, dataVar, wsVar, "Lookup"),
                "merge" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Merge"),
                "spread" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Spread"),
                "single" => func.Arguments.Count > 1 ? throw new FallbackException() : EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Single"),
                "flatten" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Flatten"),
                "shuffle" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Shuffle"),

                // Phase 1e: Error/utility
                "error" => EmitBuiltinNullaryOrUnary(sb, func, indent, dataVar, wsVar, "Error"),
                "assert" => EmitBuiltinOptionalSecond(sb, func, indent, dataVar, wsVar, "Assert"),
                "now" => EmitBuiltinNullary(sb, func, indent, wsVar, "Now"),
                "millis" => EmitBuiltinNullary(sb, func, indent, wsVar, "Millis"),

                // Phase 1f: Encoding
                "base64encode" => EmitBuiltinUnaryOrDefault(sb, func, indent, dataVar, wsVar, "Base64Encode"),
                "base64decode" => EmitBuiltinUnaryOrDefault(sb, func, indent, dataVar, wsVar, "Base64Decode"),
                "encodeUrlComponent" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "EncodeUrlComponent"),
                "decodeUrlComponent" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "DecodeUrlComponent"),
                "encodeUrl" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "EncodeUrl"),
                "decodeUrl" => EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "DecodeUrl"),

                // Phase 1g: Date/time formatting
                "fromMillis" => EmitBuiltinUpToThree(sb, func, indent, dataVar, wsVar, "FromMillis"),
                "toMillis" => EmitBuiltinOptionalSecond(sb, func, indent, dataVar, wsVar, "ToMillis"),
                "formatNumber" => EmitBuiltinOptionalThird(sb, func, indent, dataVar, wsVar, "FormatNumber"),
                "formatBase" => EmitBuiltinOptionalSecond(sb, func, indent, dataVar, wsVar, "FormatBase"),
                "formatInteger" => EmitBuiltinBinary(sb, func, indent, dataVar, wsVar, "FormatInteger"),
                "parseInteger" => EmitBuiltinBinary(sb, func, indent, dataVar, wsVar, "ParseInteger"),

                // Phase 2: Replace and Zip
                "replace" => EmitBuiltinReplace(sb, func, indent, dataVar, wsVar),
                "zip" => EmitBuiltinZip(sb, func, indent, dataVar, wsVar),

                _ => throw new FallbackException(),
            };
        }

        private string EmitBuiltinUnary(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName)
        {
            if (func.Arguments.Count != 1)
            {
                throw new FallbackException();
            }

            string arg = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({arg}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emits a unary function call. For wrong arg counts, emits T0410 throw at compile time.
        /// </summary>
        private string EmitBuiltinUnaryOrError(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName, string funcName)
        {
            if (func.Arguments.Count != 1)
            {
                // Emit the error inline — same as runtime
                throw new JsonataException("T0410", $"Arguments of function '{funcName}' do not match function signature", 0);
            }

            string arg = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({arg}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emits a unary function call. For 0 args, emits <c>default</c> (undefined).
        /// Matches runtime behavior where 0-arg base64encode/decode returns undefined.
        /// </summary>
        private string EmitBuiltinUnaryOrDefault(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName)
        {
            if (func.Arguments.Count == 0)
            {
                string v = NextVar();
                L(sb, indent, $"var {v} = default(JsonElement);");
                return v;
            }

            if (func.Arguments.Count != 1)
            {
                throw new FallbackException();
            }

            string arg = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string vr = NextVar();
            L(sb, indent, $"var {vr} = {H}.{helperName}({arg}, {wsVar});");
            return vr;
        }

        private string EmitBuiltinBoolean(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, bool negate)
        {
            string funcName = negate ? "not" : "boolean";
            if (func.Arguments.Count != 1)
            {
                throw new JsonataException("T0410", $"Arguments of function '{funcName}' do not match function signature", 0);
            }

            string arg = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string v = NextVar();
            string neg = negate ? "!" : string.Empty;

            // $boolean(undefined) → undefined (not false); $boolean(null) → false
            L(sb, indent, $"var {v} = {arg}.ValueKind == JsonValueKind.Undefined ? default : {H}.BooleanElement({neg}{H}.IsTruthy({arg}));");
            return v;
        }

        private string EmitBuiltinString(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar, string wsVar)
        {
            if (func.Arguments.Count > 2)
            {
                throw new FallbackException();
            }

            string arg = func.Arguments.Count >= 1
                ? EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar)
                : dataVar;

            if (func.Arguments.Count == 2)
            {
                string prettyArg = EmitExpression(sb, func.Arguments[1], indent, dataVar, wsVar);
                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.String({arg}, {prettyArg}, {wsVar});");
                return v;
            }
            else
            {
                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.String({arg}, {wsVar});");
                return v;
            }
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

        /// <summary>
        /// Emit a built-in that takes 0 or 1 arguments. When 0 args, passes the current
        /// data context directly (mirrors the runtime's ContextArg pattern).
        /// </summary>
        private string EmitBuiltinContextOptional(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName)
        {
            if (func.Arguments.Count > 1)
            {
                throw new FallbackException();
            }

            string arg = func.Arguments.Count == 1
                ? EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar)
                : dataVar;
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({arg}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emit a built-in that takes exactly 2 arguments.
        /// </summary>
        private string EmitBuiltinBinary(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName)
        {
            if (func.Arguments.Count != 2)
            {
                throw new FallbackException();
            }

            string arg0 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string arg1 = EmitExpression(sb, func.Arguments[1], indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({arg0}, {arg1}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emit a built-in that takes 1 required arg and 1 optional arg (e.g. $round).
        /// When the optional arg is missing, passes default (undefined) for it.
        /// </summary>
        private string EmitBuiltinOptionalSecond(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName)
        {
            if (func.Arguments.Count < 1 || func.Arguments.Count > 2)
            {
                throw new FallbackException();
            }

            string arg0 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string arg1 = func.Arguments.Count >= 2
                ? EmitExpression(sb, func.Arguments[1], indent, dataVar, wsVar)
                : "default";
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({arg0}, {arg1}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emit a built-in that takes 0 arguments (e.g. $now, $millis).
        /// </summary>
        private string EmitBuiltinNullary(
            StringBuilder sb, FunctionCallNode func, string indent,
            string wsVar, string helperName)
        {
            if (func.Arguments.Count != 0)
            {
                throw new FallbackException();
            }

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({wsVar});");
            return v;
        }

        /// <summary>
        /// Emit a built-in that takes 0 or 1 arguments. When 0 args, passes default
        /// (NOT dataVar — unlike ContextOptional). Used for $error.
        /// </summary>
        private string EmitBuiltinNullaryOrUnary(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName)
        {
            if (func.Arguments.Count > 1)
            {
                throw new FallbackException();
            }

            string arg = func.Arguments.Count == 1
                ? EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar)
                : "default";
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({arg}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emit a built-in that takes 2 required args and 1 optional arg (e.g. $substring, $pad).
        /// </summary>
        private string EmitBuiltinOptionalThird(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName)
        {
            if (func.Arguments.Count < 2 || func.Arguments.Count > 3)
            {
                throw new FallbackException();
            }

            string arg0 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string arg1 = EmitExpression(sb, func.Arguments[1], indent, dataVar, wsVar);
            string arg2 = func.Arguments.Count >= 3
                ? EmitExpression(sb, func.Arguments[2], indent, dataVar, wsVar)
                : "default";
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({arg0}, {arg1}, {arg2}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emit a built-in that takes 1-3 args (e.g. $fromMillis).
        /// </summary>
        private string EmitBuiltinUpToThree(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName)
        {
            if (func.Arguments.Count < 1 || func.Arguments.Count > 3)
            {
                throw new FallbackException();
            }

            string arg0 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string arg1 = func.Arguments.Count >= 2
                ? EmitExpression(sb, func.Arguments[1], indent, dataVar, wsVar)
                : "default";
            string arg2 = func.Arguments.Count >= 3
                ? EmitExpression(sb, func.Arguments[2], indent, dataVar, wsVar)
                : "default";
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({arg0}, {arg1}, {arg2}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emit a context-implied binary: 1-2 args where 1 arg means dataVar is the implicit first arg.
        /// Used for $contains (1 arg = context-implied string, search).
        /// </summary>
        private string EmitBuiltinContextImpliedBinary(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName)
        {
            if (func.Arguments.Count < 1 || func.Arguments.Count > 2)
            {
                throw new FallbackException();
            }

            string arg0;
            string arg1;
            if (func.Arguments.Count == 1)
            {
                arg0 = dataVar;
                arg1 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            }
            else
            {
                arg0 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
                arg1 = EmitExpression(sb, func.Arguments[1], indent, dataVar, wsVar);
            }

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.{helperName}({arg0}, {arg1}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emit $split: 1-3 args with context-implied first arg pattern.
        /// </summary>
        private string EmitBuiltinSplit(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar)
        {
            if (func.Arguments.Count < 1 || func.Arguments.Count > 3)
            {
                throw new FallbackException();
            }

            string arg0;
            string arg1;
            string arg2;
            bool contextImplied;

            if (func.Arguments.Count == 1)
            {
                arg0 = dataVar;
                arg1 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
                arg2 = "default";
                contextImplied = true;
            }
            else if (func.Arguments.Count == 2)
            {
                arg0 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
                arg1 = EmitExpression(sb, func.Arguments[1], indent, dataVar, wsVar);
                arg2 = "default";
                contextImplied = false;
            }
            else
            {
                arg0 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
                arg1 = EmitExpression(sb, func.Arguments[1], indent, dataVar, wsVar);
                arg2 = EmitExpression(sb, func.Arguments[2], indent, dataVar, wsVar);
                contextImplied = false;
            }

            string v = NextVar();
            if (contextImplied)
            {
                // Context-implied: don't guard for undefined (runtime ContextArg wraps even undefined,
                // so Split's type check fires T0410 for non-string input including undefined).
                L(sb, indent, $"var {v} = {H}.Split({arg0}, {arg1}, {arg2}, {wsVar});");
            }
            else
            {
                // Explicit first arg: undefined input returns undefined (matches runtime seq.IsUndefined check).
                L(sb, indent, $"var {v} = {arg0}.ValueKind == JsonValueKind.Undefined ? default : {H}.Split({arg0}, {arg1}, {arg2}, {wsVar});");
            }

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

            bool hasIndex = lambda.Parameters.Count >= 2 && helperName == "MapElements";
            string? idxParam = hasIndex ? $"idx_{lambdaIdx}" : null;

            string? savedVar = StashVariable(lambda.Parameters[0], elParam);
            string? savedIdx = hasIndex ? StashVariable(lambda.Parameters[1], idxParam!) : null;

            // Inside HOF body, nested lambdas (e.g. ApplyStage predicates) may reference
            // outer HOF parameters. Disable static lambdas to avoid CS8820.
            bool prevRootRef = _usesRootRef;
            _usesRootRef = true;
            string bodyResult = EmitExpression(lambdaBody, lambda.Body, innerIndent, elParam, wsParam);
            _usesRootRef = prevRootRef;

            if (hasIndex)
            {
                RestoreVariable(lambda.Parameters[1], savedIdx);
            }

            RestoreVariable(lambda.Parameters[0], savedVar);

            string v = NextVar();

            if (hasIndex)
            {
                L(sb, indent, $"var {v} = {H}.MapElementsWithIndex({inputVar}, (JsonElement {elParam}, JsonElement {idxParam}, JsonWorkspace {wsParam}) =>");
            }
            else
            {
                L(sb, indent, $"var {v} = {H}.{helperName}({inputVar}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            }

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
            bool prevRootRef = _usesRootRef;
            _usesRootRef = true;
            string bodyResult = EmitExpression(lambdaBody, lambda.Body, innerIndent, currParam, wsParam);
            _usesRootRef = prevRootRef;
            RestoreVariable(lambda.Parameters[1], savedCurr);
            RestoreVariable(lambda.Parameters[0], savedPrev);

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.ReduceElements({inputVar}, {initVar},");
            L(sb, indent + "    ", $"{Static}(JsonElement {prevParam}, JsonElement {currParam}, JsonWorkspace {wsParam}) =>");
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
            bool prevRootRef = _usesRootRef;
            _usesRootRef = true;
            string bodyResult = EmitExpression(lambdaBody, lambda.Body, innerIndent, aParam, wsParam);
            _usesRootRef = prevRootRef;
            RestoreVariable(lambda.Parameters[1], savedB);
            RestoreVariable(lambda.Parameters[0], savedA);

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.Sort({inputVar},");
            L(sb, indent + "    ", $"{Static}(JsonElement {aParam}, JsonElement {bParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent + "    ", "{");
            sb.Append(lambdaBody);
            L(sb, innerIndent, $"return {H}.IsTruthy({bodyResult});");
            L(sb, indent + "    ", "},");
            L(sb, indent + "    ", $"{wsVar});");
            return v;
        }

        // ── Filter (standalone) ──────────────────────────────
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

        /// <summary>
        /// Emits a $replace call with string pattern (not regex).
        /// Falls back to runtime if pattern arg is a regex node.
        /// </summary>
        private string EmitBuiltinReplace(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar, string wsVar)
        {
            // $replace(str, pattern, replacement [, limit])
            // Runtime also supports 2-arg context form, but that uses ~> which we can't handle
            if (func.Arguments.Count < 3 || func.Arguments.Count > 4)
            {
                if (func.Arguments.Count is 0 or 1)
                {
                    throw new JsonataException("T0410", "$replace expects 3-4 arguments", 0);
                }

                throw new FallbackException();
            }

            // If the pattern is a regex literal, fall back to runtime
            if (func.Arguments[1] is RegexNode)
            {
                throw new FallbackException();
            }

            string arg0 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string arg1 = EmitExpression(sb, func.Arguments[1], indent, dataVar, wsVar);
            string arg2 = EmitExpression(sb, func.Arguments[2], indent, dataVar, wsVar);
            string arg3 = func.Arguments.Count >= 4
                ? EmitExpression(sb, func.Arguments[3], indent, dataVar, wsVar)
                : "default";

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.Replace({arg0}, {arg1}, {arg2}, {arg3}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emits a $zip call with variable number of arguments.
        /// </summary>
        private string EmitBuiltinZip(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar, string wsVar)
        {
            if (func.Arguments.Count == 0)
            {
                string undef = NextVar();
                L(sb, indent, $"var {undef} = default(JsonElement);");
                return undef;
            }

            // Emit all args and build the array
            var argVars = new List<string>();
            foreach (var arg in func.Arguments)
            {
                argVars.Add(EmitExpression(sb, arg, indent, dataVar, wsVar));
            }

            string argsArray = string.Join(", ", argVars);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.Zip(new JsonElement[] {{ {argsArray} }}, {wsVar});");
            return v;
        }

        private static bool IsBuiltinFunctionName(string name)
        {
            return name is "sum" or "count" or "string" or "boolean" or "not"
                or "join" or "map" or "filter" or "reduce" or "sort" or "length"
                or "max" or "min" or "average" or "append" or "reverse" or "each"
                or "keys" or "values" or "spread" or "merge" or "type" or "exists"
                or "lookup" or "match" or "replace" or "contains" or "split"
                or "trim" or "pad" or "uppercase" or "lowercase" or "substring"
                or "substringBefore" or "substringAfter" or "number" or "abs"
                or "floor" or "ceil" or "round" or "power" or "sqrt" or "random"
                or "millis" or "now" or "fromMillis" or "toMillis" or "base64encode"
                or "base64decode" or "encodeUrlComponent" or "encodeUrl"
                or "decodeUrlComponent" or "decodeUrl" or "formatNumber"
                or "formatBase" or "formatInteger" or "parseInteger" or "eval"
                or "clone" or "error" or "assert" or "sift" or "zip" or "single";
        }
    }
}