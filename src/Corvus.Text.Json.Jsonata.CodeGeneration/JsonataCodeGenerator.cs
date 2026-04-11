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
        L(sb, string.Empty, "using System;");
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

        /// <summary>
        /// CSE cache for property navigation steps.
        /// Key: (sourceVarName, propertyName) → resultVarName.
        /// When the same property is navigated from the same source variable,
        /// the result is reused instead of emitting a redundant navigation.
        /// </summary>
        private readonly Dictionary<(string, string), string> _propertyStepCache = new();

        private int _varCounter;
        private int _nameFieldCounter;
        private int _pathFieldCounter;
        private int _predicateFieldCounter;
        private int _lambdaCounter;

        /// <summary>
        /// When set to a data variable name, <see cref="EmitName"/> and
        /// <see cref="EmitSimplePropertyChain"/> emit inline
        /// <c>TryGetProperty</c> instead of <c>NavigateProperty</c>. This
        /// avoids the function-call overhead and the Array auto-map branch
        /// when the data source is known to be a per-element variable from
        /// an array iteration (e.g. inside the fused array-of-objects callback).
        /// </summary>
        private string? _knownObjectDataVar;

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
        /// Gets or creates a static <c>byte[][]</c> chain field from a range of name steps.
        /// </summary>
        private string GetOrCreateChainField(List<JsonataNode> steps, int start, int end)
        {
            int count = end - start;
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = ((NameNode)steps[start + i]).Value;
            }

            return CreatePathField(names);
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

            // Check for property chain with constant-index or string-equality predicates
            // (e.g. Contact.Phone[type = 'mobile'].number, items[0].name)
            if (path.Annotations?.Group is null
                && !path.KeepArray && !path.KeepSingletonArray
                && TryBuildFusedPropertyChain(steps, out var fusedNames, out var fusedIndices, out var fusedPreds))
            {
                string result = EmitFusedPropertyChain(sb, fusedNames, fusedIndices, fusedPreds, indent, dataVar, wsVar);

                if (steps.Exists(s => s.KeepArray))
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
                        // Fusion: if the chain is followed by a computed step,
                        // use fused chain+operation helpers that avoid intermediate CreateArrayBuilder.
                        if (i < steps.Count
                            && steps[i] is not NameNode
                            && steps[i] is not ArrayConstructorNode { ConsArray: true, Expressions.Count: 0 }
                            && steps[i] is not WildcardNode
                            && steps[i] is not DescendantNode
                            && steps[i] is not SortNode
                            && !HasComplexAnnotations(steps[i])
                            && i > 0  // computed step must not be the first step
                            && (i - segStart) >= 2) // chain must be 2+ steps to benefit from fusion
                        {
                            string? fused = TryEmitFusedChainStep(
                                sb, steps, segStart, i, steps[i], indent, currentVar, wsVar);
                            if (fused != null)
                            {
                                currentVar = fused;
                                i++; // consumed the computed step too
                                continue;
                            }
                        }

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
                    if (HasComplexAnnotations(step))
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

                    // Apply stages (e.g. [type="home"] predicate) after wildcard/descendant
                    if (HasStages(step))
                    {
                        foreach (JsonataNode stage in step.Annotations!.Stages)
                        {
                            currentVar = EmitFilterStage(sb, stage, currentVar, indent, wsVar);
                        }
                    }

                    i++;
                }
                else if (step is SortNode sortNode)
                {
                    if (HasComplexAnnotations(step))
                    {
                        throw new FallbackException();
                    }

                    currentVar = EmitSortStep(sb, sortNode, currentVar, indent, dataVar, wsVar);

                    // Apply stages (e.g. [0] predicate) after sorting
                    if (HasStages(step))
                    {
                        foreach (JsonataNode stage in step.Annotations!.Stages)
                        {
                            currentVar = EmitFilterStage(sb, stage, currentVar, indent, wsVar);
                        }
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

        /// <summary>
        /// If <paramref name="node"/> is a simple property chain PathNode with 2+ name steps
        /// (no predicates/annotations), creates and returns the chain field name for use with
        /// fused chain helpers. Returns <c>null</c> otherwise.
        /// </summary>
        private string? TryGetSimpleChainField(JsonataNode node)
        {
            if (node is not PathNode path)
            {
                return null;
            }

            if (path.Steps.Count < 2)
            {
                return null;
            }

            if (!IsSimplePropertyChain(path.Steps))
            {
                return null;
            }

            if (path.Annotations?.Group is not null || path.KeepArray || path.KeepSingletonArray)
            {
                return null;
            }

            return GetOrCreateChainField(path.Steps, 0, path.Steps.Count);
        }

        /// <summary>
        /// Attempts to detect a property chain with optional constant-index or
        /// string-equality predicates that can be fused into a single helper call.
        /// Mirrors the runtime's <c>TryCompileSimplePropertyChain</c>.
        /// </summary>
        private static bool TryBuildFusedPropertyChain(
            List<JsonataNode> steps,
            out string[] propertyNames,
            out int[]? constantIndices,
            out (string PropName, string[] ExpectedValues)[]? equalityPredicates)
        {
            propertyNames = new string[steps.Count];
            constantIndices = null;
            equalityPredicates = null;
            bool hasPredicates = false;

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] is not NameNode nameNode)
                {
                    return false;
                }

                if (HasComplexAnnotations(nameNode) || HasGroupAnnotation(nameNode))
                {
                    return false;
                }

                propertyNames[i] = nameNode.Value;

                if (HasStages(nameNode))
                {
                    var stages = nameNode.Annotations!.Stages;
                    if (stages.Count != 1)
                    {
                        return false;
                    }

                    if (stages[0] is FilterNode { Expression: NumberNode numNode }
                        && numNode.Value >= 0
                        && numNode.Value <= int.MaxValue
                        && numNode.Value == Math.Floor(numNode.Value))
                    {
                        // Constant non-negative integer index
                        if (constantIndices is null)
                        {
                            constantIndices = new int[steps.Count];
#if NETSTANDARD2_0
                            for (int j = 0; j < constantIndices.Length; j++)
                            {
                                constantIndices[j] = -1;
                            }
#else
                            Array.Fill(constantIndices, -1);
#endif
                        }

                        constantIndices[i] = (int)numNode.Value;
                        hasPredicates = true;
                    }
                    else if (stages[0] is FilterNode filterNode
                             && TryExtractEqualityPredicateValues(filterNode.Expression, out var filterPropName, out var filterValues))
                    {
                        // String equality predicate (possibly OR-ed)
                        if (equalityPredicates is null)
                        {
                            equalityPredicates = new (string, string[])[steps.Count];
                        }

                        equalityPredicates[i] = (filterPropName, filterValues.ToArray());
                        hasPredicates = true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (constantIndices is not null)
                {
                    constantIndices[i] = -1;
                }
            }

            if (!hasPredicates)
            {
                return false;
            }

            // Ensure constantIndices is fully initialized
            if (constantIndices is null)
            {
                constantIndices = new int[steps.Count];
#if NETSTANDARD2_0
                for (int j = 0; j < constantIndices.Length; j++)
                {
                    constantIndices[j] = -1;
                }
#else
                Array.Fill(constantIndices, -1);
#endif
            }

            return true;
        }

        /// <summary>
        /// Recursively extracts equality predicate values from an expression tree.
        /// Handles <c>prop = 'value'</c> and <c>prop = 'a' or prop = 'b'</c>.
        /// Mirrors the runtime's <c>TryExtractEqualityPredicateValues</c>.
        /// </summary>
        private static bool TryExtractEqualityPredicateValues(
            JsonataNode expression,
            out string propName,
            out List<string> values)
        {
            // Single equality: prop = 'value'
            if (expression is BinaryNode { Operator: "=" } eq
                && TryGetSimpleNameNode(eq.Lhs, out propName)
                && eq.Rhs is StringNode strNode)
            {
                values = [strNode.Value];
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
        /// Extracts a bare property name from a NameNode or a PathNode wrapping a single NameNode.
        /// Mirrors the runtime's <c>TryGetSimpleNameNode</c>.
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
                && pathNode.Annotations is null
                && innerName.Annotations is null)
            {
                name = innerName.Value;
                return true;
            }

            name = default!;
            return false;
        }

        /// <summary>
        /// Emits a fused property chain call with pre-encoded predicate data.
        /// When the chain has a single equality predicate (no constant indices),
        /// emits inline filter code that avoids ElementBuffer for the common
        /// 0-or-1-match case. Falls back to the helper for complex cases
        /// (multiple predicates, constant indices, or array input).
        /// </summary>
        private string EmitFusedPropertyChain(
            StringBuilder sb,
            string[] propertyNames,
            int[]? constantIndices,
            (string PropName, string[] ExpectedValues)[]? equalityPredicates,
            string indent,
            string dataVar,
            string wsVar)
        {
            // Create the path field (byte[][]) — needed for fallback
            string pathField = CreatePathField(propertyNames);

            // Create constant indices field
            string indicesExpr;
            bool hasAnyIndex = constantIndices is not null && Array.Exists(constantIndices, i => i >= 0);
            if (hasAnyIndex)
            {
                string idxField = $"s_ci{_predicateFieldCounter++}";
                string values = string.Join(", ", constantIndices!);
                _staticFieldDeclarations.Add(
                    $"private static readonly int[] {idxField} = new int[] {{ {values} }};");
                indicesExpr = idxField;
            }
            else
            {
                indicesExpr = "null";
            }

            // Create equality predicates field
            string predsExpr;
            bool hasAnyEqPred = equalityPredicates is not null
                                && Array.Exists(equalityPredicates, p => p.PropName is not null);
            if (hasAnyEqPred)
            {
                string predField = $"s_ep{_predicateFieldCounter++}";
                var sb2 = new StringBuilder();
                sb2.Append($"private static readonly (byte[], byte[][])[] {predField} = new (byte[], byte[][])[] {{ ");

                for (int i = 0; i < equalityPredicates!.Length; i++)
                {
                    if (i > 0)
                    {
                        sb2.Append(", ");
                    }

                    if (equalityPredicates[i].PropName is not null)
                    {
                        string propNameField = GetOrCreateNameField(equalityPredicates[i].PropName);
                        var valueFields = new string[equalityPredicates[i].ExpectedValues.Length];
                        for (int v = 0; v < valueFields.Length; v++)
                        {
                            valueFields[v] = GetOrCreateNameField(equalityPredicates[i].ExpectedValues[v]);
                        }

                        sb2.Append($"({propNameField}, new byte[][] {{ {string.Join(", ", valueFields)} }})");
                    }
                    else
                    {
                        sb2.Append("(null!, null!)");
                    }
                }

                sb2.Append(" };");
                _staticFieldDeclarations.Add(sb2.ToString());
                predsExpr = predField;
            }
            else
            {
                predsExpr = "null";
            }

            // Try to emit inline predicate chain (single equality predicate, no constant indices)
            if (TryEmitInlinePredicateChain(
                sb, propertyNames, constantIndices, equalityPredicates,
                indent, dataVar, wsVar, pathField, indicesExpr, predsExpr, out string inlineResult))
            {
                return inlineResult;
            }

            // Fallback: opaque helper call
            string v2 = NextVar();
            L(sb, indent, $"var {v2} = {H}.NavigatePropertyChainWithPredicates({dataVar}, {pathField}, {indicesExpr}, {predsExpr}, {wsVar});");
            return v2;
        }

        /// <summary>
        /// Tries to emit an inline predicate chain that avoids the helper call entirely.
        /// Handles chains with exactly one equality predicate step and no constant indices.
        /// The inline path handles the common object-navigation case; when a pre-predicate
        /// step returns an array, emits an outer iteration loop. Falls back to the helper
        /// for array-at-input or other unsupported patterns.
        /// </summary>
        private bool TryEmitInlinePredicateChain(
            StringBuilder sb,
            string[] propertyNames,
            int[]? constantIndices,
            (string PropName, string[] ExpectedValues)[]? equalityPredicates,
            string indent,
            string dataVar,
            string wsVar,
            string pathField,
            string indicesExpr,
            string predsExpr,
            out string resultVar)
        {
            // Only inline when we have exactly one equality predicate and no constant indices
            if (equalityPredicates is null)
            {
                resultVar = default!;
                return false;
            }

            bool hasConstIdx = constantIndices is not null && Array.Exists(constantIndices, i => i >= 0);
            if (hasConstIdx)
            {
                resultVar = default!;
                return false;
            }

            int predStepIdx = -1;
            for (int i = 0; i < equalityPredicates.Length; i++)
            {
                if (equalityPredicates[i].PropName is not null)
                {
                    if (predStepIdx >= 0)
                    {
                        // Multiple equality predicates — too complex for inline
                        resultVar = default!;
                        return false;
                    }

                    predStepIdx = i;
                }
            }

            if (predStepIdx < 0)
            {
                resultVar = default!;
                return false;
            }

            var pred = equalityPredicates[predStepIdx];
            resultVar = NextVar();

            L(sb, indent, $"JsonElement {resultVar} = default;");

            if (predStepIdx == 0)
            {
                // No pre-predicate steps. The predicate step's source is dataVar.
                // Build flat condition: data.ValueKind == Object && data.TryGetProperty(predStep)
                string escaped0 = EscapeStringLiteral(propertyNames[0]);
                const string predArrayVar = "__ip0";

                L(sb, indent, $"if ({dataVar}.ValueKind == JsonValueKind.Object && {dataVar}.TryGetProperty(\"{escaped0}\"u8, out var {predArrayVar}))");
                L(sb, indent, "{");
                string i1 = indent + "    ";
                string i2 = i1 + "    ";

                EmitInlinePredicateHandling(sb, i1, i2, predArrayVar, pred, propertyNames, predStepIdx, resultVar, wsVar);

                L(sb, indent, "}");
            }
            else
            {
                // Has pre-predicate steps. Build condition for steps 0 through predStepIdx-1,
                // then inline-handle predicate when source is Object. If the source turns
                // out to be Array (auto-map), the else falls through to the helper which
                // handles CollectAndContinue via FusedCollectAndContinue.
                var preCond = new List<string>();
                string prevVar = dataVar;

                for (int i = 0; i < predStepIdx; i++)
                {
                    string escaped = EscapeStringLiteral(propertyNames[i]);
                    string stepVar = $"__ip{i}";
                    preCond.Add($"{prevVar}.ValueKind == JsonValueKind.Object && {prevVar}.TryGetProperty(\"{escaped}\"u8, out var {stepVar})");
                    prevVar = stepVar;
                }

                // prevVar is now __ip{predStepIdx-1} — the source of the predicate step.
                // Add source-is-Object + TryGetProperty for predicate step to the same condition
                // so that Arrays at this level fall through to the helper.
                string sourceVar = prevVar;
                string predStepEscaped = EscapeStringLiteral(propertyNames[predStepIdx]);
                string predArrayVar = $"__ip{predStepIdx}";
                preCond.Add($"{sourceVar}.ValueKind == JsonValueKind.Object && {sourceVar}.TryGetProperty(\"{predStepEscaped}\"u8, out var {predArrayVar})");

                L(sb, indent, $"if ({string.Join($"\n{indent}    && ", preCond)})");
                L(sb, indent, "{");
                string i1 = indent + "    ";

                EmitInlinePredicateHandling(sb, i1, i1 + "    ", predArrayVar, pred, propertyNames, predStepIdx, resultVar, wsVar);

                L(sb, indent, "}");
            }

            // Fallback for array input or arrays at intermediate non-predicate steps
            L(sb, indent, "else");
            L(sb, indent, "{");
            L(sb, indent + "    ", $"{resultVar} = {H}.NavigatePropertyChainWithPredicates({dataVar}, {pathField}, {indicesExpr}, {predsExpr}, {wsVar});");
            L(sb, indent, "}");

            return true;
        }

        /// <summary>
        /// Emits the predicate handling (array filter or singleton check) for the predicate
        /// step variable. Used for both the direct (Object source) and outer-array cases.
        /// </summary>
        private void EmitInlinePredicateHandling(
            StringBuilder sb,
            string indent,
            string innerIndent,
            string predArrayVar,
            (string PropName, string[] ExpectedValues) pred,
            string[] propertyNames,
            int predStepIdx,
            string resultVar,
            string wsVar)
        {
            // Array case: filter loop
            L(sb, indent, $"if ({predArrayVar}.ValueKind == JsonValueKind.Array)");
            L(sb, indent, "{");

            EmitInlineFilterLoop(sb, innerIndent, predArrayVar, pred, propertyNames, predStepIdx, resultVar, wsVar);

            L(sb, indent, "}");

            // Singleton object case: check predicate directly
            L(sb, indent, $"else if ({predArrayVar}.ValueKind == JsonValueKind.Object)");
            L(sb, indent, "{");

            EmitInlineSingletonCheck(sb, innerIndent, predArrayVar, pred, propertyNames, predStepIdx, resultVar);

            L(sb, indent, "}");
        }

        /// <summary>
        /// Emits an outer loop over an array source for the predicate step.
        /// When a pre-predicate step returns an array (e.g., Contact is array of 3 contacts),
        /// iterates each element, navigates to the predicate step's property, and applies the
        /// predicate filter + post-predicate chain inline, collecting all results across all
        /// outer elements using the same first-match-direct / ElementBuffer pattern.
        /// </summary>
        private void EmitInlineOuterArrayLoop(
            StringBuilder sb,
            string indent,
            string outerArrayVar,
            string predStepEscapedName,
            (string PropName, string[] ExpectedValues) pred,
            string[] propertyNames,
            int predStepIdx,
            string resultVar,
            string wsVar)
        {
            L(sb, indent, "JsonElement __ipFirst = default;");
            L(sb, indent, "int __ipMc = 0;");
            L(sb, indent, "var __ipBuf = default(ElementBuffer);");
            L(sb, indent, "try");
            L(sb, indent, "{");
            string i1 = indent + "    ";

            L(sb, i1, $"foreach (var __ipOuter in {outerArrayVar}.EnumerateArray())");
            L(sb, i1, "{");
            string i2 = i1 + "    ";

            L(sb, i2, $"if (__ipOuter.ValueKind == JsonValueKind.Object && __ipOuter.TryGetProperty(\"{predStepEscapedName}\"u8, out var __ipInner))");
            L(sb, i2, "{");
            string i3 = i2 + "    ";

            // Inner: __ipInner might be Array (filter loop) or Object (singleton check)
            L(sb, i3, "if (__ipInner.ValueKind == JsonValueKind.Array)");
            L(sb, i3, "{");
            string i4 = i3 + "    ";

            // Inline filter loop body (reusing shared accumulation variables)
            EmitInlineFilterLoopBody(sb, i4, pred, propertyNames, predStepIdx);

            L(sb, i3, "}");
            L(sb, i3, "else if (__ipInner.ValueKind == JsonValueKind.Object)");
            L(sb, i3, "{");

            // Singleton check body (reusing shared accumulation variables)
            EmitInlineSingletonCheckBody(sb, i4, pred, propertyNames, predStepIdx);

            L(sb, i3, "}");
            L(sb, i2, "}");
            L(sb, i1, "}");

            // Materialize result from shared accumulation
            L(sb, i1, $"{resultVar} = __ipMc == 0 ? default : __ipMc == 1 ? __ipFirst : __ipBuf.ToResult({wsVar});");

            L(sb, indent, "}");
            L(sb, indent, "finally");
            L(sb, indent, "{");
            L(sb, indent + "    ", "__ipBuf.Dispose();");
            L(sb, indent, "}");
        }

        /// <summary>
        /// Emits the body of the inline filter loop (the foreach over the array + predicate check
        /// + collect), using shared accumulation variables (__ipFirst, __ipMc, __ipBuf).
        /// The caller must have already declared these variables.
        /// </summary>
        private void EmitInlineFilterLoopBody(
            StringBuilder sb,
            string indent,
            (string PropName, string[] ExpectedValues) pred,
            string[] propertyNames,
            int predStepIdx)
        {
            string i1 = indent + "    ";

            L(sb, indent, "foreach (var __ipEl in __ipInner.EnumerateArray())");
            L(sb, indent, "{");

            var condParts = BuildPredicateConditionParts(pred, propertyNames, predStepIdx, "__ipEl", out string matchResultVar);

            L(sb, i1, $"if ({string.Join($"\n{i1}    && ", condParts)})");
            L(sb, i1, "{");
            string i2 = i1 + "    ";
            L(sb, i2, $"if (__ipMc == 0) {{ __ipFirst = {matchResultVar}; __ipMc = 1; }}");
            L(sb, i2, $"else {{ if (__ipMc == 1) {{ __ipBuf.Add(__ipFirst); }} __ipBuf.Add({matchResultVar}); __ipMc++; }}");
            L(sb, i1, "}");

            L(sb, indent, "}");
        }

        /// <summary>
        /// Emits the body of the singleton predicate check, using shared accumulation
        /// variables (__ipFirst, __ipMc, __ipBuf).
        /// </summary>
        private void EmitInlineSingletonCheckBody(
            StringBuilder sb,
            string indent,
            (string PropName, string[] ExpectedValues) pred,
            string[] propertyNames,
            int predStepIdx)
        {
            string i1 = indent + "    ";

            var condParts = BuildSingletonPredicateConditionParts(pred, propertyNames, predStepIdx, "__ipInner", out string matchResultVar);

            L(sb, indent, $"if ({string.Join($"\n{indent}    && ", condParts)})");
            L(sb, indent, "{");
            L(sb, i1, $"if (__ipMc == 0) {{ __ipFirst = {matchResultVar}; __ipMc = 1; }}");
            L(sb, i1, $"else {{ if (__ipMc == 1) {{ __ipBuf.Add(__ipFirst); }} __ipBuf.Add({matchResultVar}); __ipMc++; }}");
            L(sb, indent, "}");
        }

        /// <summary>
        /// Builds the condition parts for checking the predicate and navigating post-predicate
        /// steps on an array element. Used by both the filter loop and the outer array loop.
        /// </summary>
        private List<string> BuildPredicateConditionParts(
            (string PropName, string[] ExpectedValues) pred,
            string[] propertyNames,
            int predStepIdx,
            string elementVar,
            out string matchResultVar)
        {
            var condParts = new List<string>();
            string escapedPred = EscapeStringLiteral(pred.PropName);
            condParts.Add($"{elementVar}.ValueKind == JsonValueKind.Object");
            condParts.Add($"{elementVar}.TryGetProperty(\"{escapedPred}\"u8, out var __ipPv)");
            condParts.Add("__ipPv.ValueKind == JsonValueKind.String");

            AddExpectedValueChecks(condParts, pred, "__ipPv");

            // Post-predicate chain
            int postSteps = propertyNames.Length - predStepIdx - 1;
            if (postSteps == 0)
            {
                matchResultVar = elementVar;
            }
            else
            {
                string prevPost = elementVar;
                for (int i = predStepIdx + 1; i < propertyNames.Length; i++)
                {
                    string escaped = EscapeStringLiteral(propertyNames[i]);
                    string postVar = i == propertyNames.Length - 1 ? "__ipR" : $"__ipP{i}";
                    condParts.Add($"{prevPost}.TryGetProperty(\"{escaped}\"u8, out var {postVar})");
                    prevPost = postVar;
                }

                matchResultVar = prevPost;
            }

            return condParts;
        }

        /// <summary>
        /// Builds the condition parts for the singleton predicate check.
        /// </summary>
        private List<string> BuildSingletonPredicateConditionParts(
            (string PropName, string[] ExpectedValues) pred,
            string[] propertyNames,
            int predStepIdx,
            string objectVar,
            out string matchResultVar)
        {
            var condParts = new List<string>();
            string escapedPred = EscapeStringLiteral(pred.PropName);
            condParts.Add($"{objectVar}.TryGetProperty(\"{escapedPred}\"u8, out var __ipSPv)");
            condParts.Add("__ipSPv.ValueKind == JsonValueKind.String");

            AddExpectedValueChecks(condParts, pred, "__ipSPv");

            // Post-predicate chain
            int postSteps = propertyNames.Length - predStepIdx - 1;
            if (postSteps == 0)
            {
                matchResultVar = objectVar;
            }
            else
            {
                string prevPost = objectVar;
                for (int i = predStepIdx + 1; i < propertyNames.Length; i++)
                {
                    string escaped = EscapeStringLiteral(propertyNames[i]);
                    string postVar = $"__ipSR{i}";
                    condParts.Add($"{prevPost}.TryGetProperty(\"{escaped}\"u8, out var {postVar})");
                    prevPost = postVar;
                }

                matchResultVar = prevPost;
            }

            return condParts;
        }

        /// <summary>
        /// Adds expected value check condition parts (single value or OR'd values).
        /// </summary>
        private static void AddExpectedValueChecks(
            List<string> condParts,
            (string PropName, string[] ExpectedValues) pred,
            string predicateVar)
        {
            if (pred.ExpectedValues.Length == 1)
            {
                condParts.Add($"{predicateVar}.ValueEquals(\"{EscapeStringLiteral(pred.ExpectedValues[0])}\"u8)");
            }
            else
            {
                var checks = new string[pred.ExpectedValues.Length];
                for (int i = 0; i < checks.Length; i++)
                {
                    checks[i] = $"{predicateVar}.ValueEquals(\"{EscapeStringLiteral(pred.ExpectedValues[i])}\"u8)";
                }

                condParts.Add($"({string.Join(" || ", checks)})");
            }
        }

        /// <summary>
        /// Emits the inline filter loop for the array case of a predicate step.
        /// For 0 or 1 matches: no ElementBuffer, direct element assignment.
        /// For 2+ matches: uses ElementBuffer (threaded), materializes only at the end.
        /// </summary>
        private void EmitInlineFilterLoop(
            StringBuilder sb,
            string indent,
            string arrayVar,
            (string PropName, string[] ExpectedValues) pred,
            string[] propertyNames,
            int predStepIdx,
            string resultVar,
            string wsVar)
        {
            L(sb, indent, "JsonElement __ipFirst = default;");
            L(sb, indent, "int __ipMc = 0;");
            L(sb, indent, "var __ipBuf = default(ElementBuffer);");
            L(sb, indent, "try");
            L(sb, indent, "{");
            string i1 = indent + "    ";

            L(sb, i1, $"foreach (var __ipEl in {arrayVar}.EnumerateArray())");
            L(sb, i1, "{");
            string i2 = i1 + "    ";

            // Build the predicate + post-predicate condition
            var condParts = new List<string>();
            string escapedPred = EscapeStringLiteral(pred.PropName);
            condParts.Add("__ipEl.ValueKind == JsonValueKind.Object");
            condParts.Add($"__ipEl.TryGetProperty(\"{escapedPred}\"u8, out var __ipPv)");
            condParts.Add("__ipPv.ValueKind == JsonValueKind.String");

            // Expected values: single or OR'd
            if (pred.ExpectedValues.Length == 1)
            {
                condParts.Add($"__ipPv.ValueEquals(\"{EscapeStringLiteral(pred.ExpectedValues[0])}\"u8)");
            }
            else
            {
                var checks = new string[pred.ExpectedValues.Length];
                for (int i = 0; i < checks.Length; i++)
                {
                    checks[i] = $"__ipPv.ValueEquals(\"{EscapeStringLiteral(pred.ExpectedValues[i])}\"u8)";
                }

                condParts.Add($"({string.Join(" || ", checks)})");
            }

            // Post-predicate chain: TryGetProperty for each remaining step
            int postSteps = propertyNames.Length - predStepIdx - 1;
            string matchResultVar;

            if (postSteps == 0)
            {
                matchResultVar = "__ipEl";
            }
            else
            {
                string prevPost = "__ipEl";
                for (int i = predStepIdx + 1; i < propertyNames.Length; i++)
                {
                    string escaped = EscapeStringLiteral(propertyNames[i]);
                    string postVar = i == propertyNames.Length - 1 ? "__ipR" : $"__ipP{i}";
                    condParts.Add($"{prevPost}.TryGetProperty(\"{escaped}\"u8, out var {postVar})");
                    prevPost = postVar;
                }

                matchResultVar = prevPost;
            }

            L(sb, i2, $"if ({string.Join($"\n{i2}    && ", condParts)})");
            L(sb, i2, "{");
            string i3 = i2 + "    ";

            // Collect: first match directly, 2+ via ElementBuffer
            L(sb, i3, $"if (__ipMc == 0) {{ __ipFirst = {matchResultVar}; __ipMc = 1; }}");
            L(sb, i3, $"else {{ if (__ipMc == 1) {{ __ipBuf.Add(__ipFirst); }} __ipBuf.Add({matchResultVar}); __ipMc++; }}");

            L(sb, i2, "}");
            L(sb, i1, "}");

            // Materialize result
            L(sb, i1, $"{resultVar} = __ipMc == 0 ? default : __ipMc == 1 ? __ipFirst : __ipBuf.ToResult({wsVar});");

            L(sb, indent, "}");
            L(sb, indent, "finally");
            L(sb, indent, "{");
            L(sb, indent + "    ", "__ipBuf.Dispose();");
            L(sb, indent, "}");
        }

        /// <summary>
        /// Emits the inline singleton object check for the predicate step.
        /// When the predicate step's value is a single object (not an array),
        /// checks the predicate and navigates post-predicate steps directly.
        /// </summary>
        private static void EmitInlineSingletonCheck(
            StringBuilder sb,
            string indent,
            string objectVar,
            (string PropName, string[] ExpectedValues) pred,
            string[] propertyNames,
            int predStepIdx,
            string resultVar)
        {
            var condParts = new List<string>();
            string escapedPred = EscapeStringLiteral(pred.PropName);
            condParts.Add($"{objectVar}.TryGetProperty(\"{escapedPred}\"u8, out var __ipSPv)");
            condParts.Add("__ipSPv.ValueKind == JsonValueKind.String");

            // Expected values
            if (pred.ExpectedValues.Length == 1)
            {
                condParts.Add($"__ipSPv.ValueEquals(\"{EscapeStringLiteral(pred.ExpectedValues[0])}\"u8)");
            }
            else
            {
                var checks = new string[pred.ExpectedValues.Length];
                for (int i = 0; i < checks.Length; i++)
                {
                    checks[i] = $"__ipSPv.ValueEquals(\"{EscapeStringLiteral(pred.ExpectedValues[i])}\"u8)";
                }

                condParts.Add($"({string.Join(" || ", checks)})");
            }

            // Post-predicate chain
            int postSteps = propertyNames.Length - predStepIdx - 1;
            string matchResultVar;

            if (postSteps == 0)
            {
                matchResultVar = objectVar;
            }
            else
            {
                string prevPost = objectVar;
                for (int i = predStepIdx + 1; i < propertyNames.Length; i++)
                {
                    string escaped = EscapeStringLiteral(propertyNames[i]);
                    string postVar = $"__ipSR{i}";
                    condParts.Add($"{prevPost}.TryGetProperty(\"{escaped}\"u8, out var {postVar})");
                    prevPost = postVar;
                }

                matchResultVar = prevPost;
            }

            L(sb, indent, $"if ({string.Join($"\n{indent}    && ", condParts)})");
            L(sb, indent, "{");
            L(sb, indent + "    ", $"{resultVar} = {matchResultVar};");
            L(sb, indent, "}");
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

                if (_knownObjectDataVar != null && dataVar == _knownObjectDataVar)
                {
                    string tmp = NextVar();
                    L(sb, indent, $"var {v} = {dataVar}.ValueKind == JsonValueKind.Object && {dataVar}.TryGetProperty((ReadOnlySpan<byte>){nameField}, out var {tmp}) ? {tmp} : default;");
                }
                else
                {
                    L(sb, indent, $"var {v} = {H}.NavigateProperty({dataVar}, {nameField}, {wsVar});");
                }

                return v;
            }

            return EmitInlinePropertyChain(sb, steps, 0, steps.Count, indent, dataVar, wsVar);
        }

        private string EmitPropertyChainSegment(
            StringBuilder sb, List<JsonataNode> steps, int start, int end,
            string indent, string currentVar, string wsVar)
        {
            int count = end - start;
            if (count == 1)
            {
                string name = ((NameNode)steps[start]).Value;

                // CSE: reuse if this (source, property) was already navigated.
                var cacheKey = (currentVar, name);
                if (_propertyStepCache.TryGetValue(cacheKey, out string? cached))
                {
                    return cached;
                }

                string nameField = GetOrCreateNameField(name);
                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.NavigateProperty({currentVar}, {nameField}, {wsVar});");
                _propertyStepCache[cacheKey] = v;
                return v;
            }

            return EmitInlinePropertyChain(sb, steps, start, end, indent, currentVar, wsVar);
        }

        /// <summary>
        /// Emits inline property chain navigation. For short chains (2 steps), uses
        /// per-step inline with <c>NavigateProperty</c> fallback per step. For longer
        /// chains (3+), uses a chained <c>&amp;&amp;</c> condition for the all-objects
        /// fast path with a flat <c>NavigatePropertyChain</c> fallback that uses at most
        /// one ArrayBuilder for the entire remaining chain.
        /// </summary>
        private string EmitInlinePropertyChain(
            StringBuilder sb, List<JsonataNode> steps, int start, int end,
            string indent, string dataVar, string wsVar)
        {
            int count = end - start;

            if (count <= 2)
            {
                return EmitPerStepInlineChain(sb, steps, start, end, indent, dataVar, wsVar);
            }

            return EmitAndChainWithFlatFallback(sb, steps, start, end, indent, dataVar, wsVar);
        }

        /// <summary>
        /// Per-step inline: each step independently checks ValueKind == Object and
        /// TryGetProperty, falling back to NavigateProperty for that single step.
        /// Optimal for short chains where nested arrays are unlikely.
        /// Uses CSE cache to reuse previously computed property navigation results.
        /// </summary>
        private string EmitPerStepInlineChain(
            StringBuilder sb, List<JsonataNode> steps, int start, int end,
            string indent, string dataVar, string wsVar)
        {
            string prevVar = dataVar;

            for (int i = start; i < end; i++)
            {
                string name = ((NameNode)steps[i]).Value;

                // CSE: if this exact (source, property) pair was already navigated, reuse.
                var cacheKey = (prevVar, name);
                if (_propertyStepCache.TryGetValue(cacheKey, out string? cached))
                {
                    prevVar = cached;
                    continue;
                }

                string escapedName = EscapeStringLiteral(name);
                string nameField = GetOrCreateNameField(name);
                string resultVar = NextVar();

                L(sb, indent, $"JsonElement {resultVar};");
                L(sb, indent, $"if ({prevVar}.ValueKind == JsonValueKind.Object && {prevVar}.TryGetProperty(\"{escapedName}\"u8, out {resultVar}))");
                L(sb, indent, "{");
                L(sb, indent, "}");
                L(sb, indent, "else");
                L(sb, indent, "{");
                L(sb, indent, $"    {resultVar} = {H}.NavigateProperty({prevVar}, {nameField}, {wsVar});");
                L(sb, indent, "}");
                L(sb, indent, "");

                _propertyStepCache[cacheKey] = resultVar;
                prevVar = resultVar;
            }

            return prevVar;
        }

        /// <summary>
        /// Nested-if chain: each step checks ValueKind == Object and TryGetProperty.
        /// On failure at step <c>k</c>, falls back to
        /// <c>NavigatePropertyChain(lastResolvedValue, chain, k, workspace)</c>
        /// which resumes navigation from step <c>k</c>, avoiding re-navigation of
        /// already-resolved prefix steps.
        /// </summary>
        private string EmitAndChainWithFlatFallback(
            StringBuilder sb, List<JsonataNode> steps, int start, int end,
            string indent, string dataVar, string wsVar)
        {
            int count = end - start;

            // Build the byte[][] chain field for the fallback paths.
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = ((NameNode)steps[start + i]).Value;
            }

            string chainField = CreatePathField(names);

            // Result variable — assigned by the fast path or fallback.
            string resultVar = NextVar();
            L(sb, indent, $"JsonElement {resultVar};");

            // Emit nested ifs: each level resolves one step, and on failure
            // calls NavigatePropertyChain with the appropriate startIndex.
            string prevVar = dataVar;
            string[] chainVars = new string[count];

            for (int i = 0; i < count; i++)
            {
                string escapedName = EscapeStringLiteral(names[i]);
                string outVar;

                if (i < count - 1)
                {
                    outVar = $"__chain{_varCounter++}";
                    L(sb, indent, $"if ({prevVar}.ValueKind == JsonValueKind.Object && {prevVar}.TryGetProperty(\"{escapedName}\"u8, out var {outVar}))");
                }
                else
                {
                    // Last step: assign to the result variable directly
                    outVar = resultVar;
                    L(sb, indent, $"if ({prevVar}.ValueKind == JsonValueKind.Object && {prevVar}.TryGetProperty(\"{escapedName}\"u8, out {outVar}))");
                }

                L(sb, indent, "{");
                chainVars[i] = outVar;
                prevVar = outVar;
                indent += "    ";
            }

            // Innermost block: all steps succeeded — nothing more to do (resultVar is set)
            indent = indent.Substring(0, indent.Length - 4); // back one level

            // Close each nesting level with an else that falls back to NavigatePropertyChain
            for (int i = count - 1; i >= 0; i--)
            {
                L(sb, indent, "}");
                L(sb, indent, "else");
                L(sb, indent, "{");

                if (i == 0)
                {
                    // First step failed — navigate full chain from dataVar
                    L(sb, indent, $"    {resultVar} = {H}.NavigatePropertyChain({dataVar}, {chainField}, {wsVar});");
                }
                else if (i == count - 1)
                {
                    // Last step failed — just navigate single property from previous chain var
                    string nameField = GetOrCreateNameField(names[i]);
                    L(sb, indent, $"    {resultVar} = {H}.NavigateProperty({chainVars[i - 1]}, {nameField}, {wsVar});");
                }
                else
                {
                    // Middle step failed — navigate remaining chain from previous chain var
                    L(sb, indent, $"    {resultVar} = {H}.NavigatePropertyChain({chainVars[i - 1]}, {chainField}, {i}, {wsVar});");
                }

                L(sb, indent, "}");
                if (i > 0)
                {
                    indent = indent.Substring(0, indent.Length - 4); // back one level
                }
            }

            L(sb, indent, "");

            return resultVar;
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
            // Single-pair groupby: both key and value are simple property names.
            if (objCtor.Pairs.Count == 1)
            {
                (JsonataNode keyExpr, JsonataNode valExpr) = objCtor.Pairs[0];

                // Fast path: both key and value are simple property names.
                // Use direct TryGetProperty — no Dictionary, no List, no lambda dispatch.
                if (TryGetSimpleNameNode(keyExpr, out string? keyPropName)
                    && TryGetSimpleNameNode(valExpr, out string? valPropName))
                {
                    string keyField = GetOrCreateNameField(keyPropName);
                    string valField = GetOrCreateNameField(valPropName);
                    string v = NextVar();
                    L(sb, indent, $"var {v} = {H}.SimpleGroupByPerElement({currentVar}, {keyField}, {valField}, {wsVar});");
                    return v;
                }
            }

            // Multi-pair with all-StringNode keys: per-element object construction via
            // MapElements + EmitObjectConstructor. StringNode keys are literal property names
            // in the constructed object, and values are evaluated per element.
            {
                bool allStringKeys = true;
                foreach ((JsonataNode key, _) in objCtor.Pairs)
                {
                    if (key is not StringNode) { allStringKeys = false; break; }
                }

                if (allStringKeys)
                {
                    int lambdaIdx = _lambdaCounter++;
                    string elParam = $"el_{lambdaIdx}";
                    string wsParam = $"ws_{lambdaIdx}";
                    string innerIndent = indent + "    ";
                    StringBuilder lambdaBody = new();

                    string objResult = EmitObjectConstructor(lambdaBody, objCtor, innerIndent, elParam, wsParam);

                    string v = NextVar();
                    L(sb, indent, $"var {v} = {H}.GroupByMapElements({currentVar}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
                    L(sb, indent, "{");
                    sb.Append(lambdaBody);
                    L(sb, innerIndent, $"return {objResult};");
                    L(sb, indent, $"}}, {wsVar});");
                    return v;
                }
            }

            // General case: lambda-based approach for NameNode keys.
            // Each element gets its own GroupByObject call producing a single-entry object.
            // GroupByObjectPerElement collects these into an array with singleton semantics.
            // KeepArray (the [] modifier) is handled by the value expression itself,
            // matching the runtime where WrapKeepArray is part of the compiled evaluator.
            if (objCtor.Pairs.Count != 1)
            {
                throw new FallbackException();
            }

            {
                (JsonataNode keyExpr, JsonataNode valExpr) = objCtor.Pairs[0];

                int lambdaIdx = _lambdaCounter++;
                string elParam = $"el_{lambdaIdx}";
                string wsParam = $"ws_{lambdaIdx}";
                string innerIndent = indent + "        ";

                StringBuilder keySb = new();
                string keyVar = EmitExpression(keySb, keyExpr, innerIndent, elParam, wsParam);

                StringBuilder valSb = new();
                string valVar = EmitExpression(valSb, valExpr, innerIndent, elParam, wsParam);

                string v2 = NextVar();

                L(sb, indent, $"var {v2} = {H}.GroupByObjectPerElement({currentVar},");
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
                return v2;
            }
        }

        private string EmitGroupByAnnotation(
            StringBuilder sb, GroupBy group, string currentVar, string indent, string wsVar)
        {
            if (group.Pairs.Count != 1)
            {
                throw new FallbackException();
            }

            (JsonataNode keyExpr, JsonataNode valExpr) = group.Pairs[0];

            // Fast path: both key and value are simple property names.
            // Uses ArrayPool + O(n²) grouping — no Dictionary, no List, no lambda dispatch.
            if (TryGetSimpleNameNode(keyExpr, out string? keyPropName)
                && TryGetSimpleNameNode(valExpr, out string? valPropName))
            {
                string keyField = GetOrCreateNameField(keyPropName);
                string valField = GetOrCreateNameField(valPropName);
                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.SimpleGroupByAnnotation({currentVar}, {keyField}, {valField}, {wsVar});");
                return v;
            }

            // General case: lambda-based approach.
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

            string v2 = NextVar();
            L(sb, indent, $"var {v2} = {H}.GroupByObject({currentVar},");
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
            return v2;
        }

        /// <summary>
        /// Emits a sort step: ^(key1, >key2, ...).
        /// Generates sort key extractor lambdas and calls H.SortByKeys.
        /// </summary>
        private string EmitSortStep(
            StringBuilder sb, SortNode sortNode, string currentVar, string indent,
            string dataVar, string wsVar)
        {
            if (sortNode.Terms.Count == 0)
            {
                return currentVar;
            }

            // Emit key extractor lambdas for each sort term
            var extractorExprs = new List<string>();
            var descendingValues = new List<bool>();

            foreach (SortTerm term in sortNode.Terms)
            {
                descendingValues.Add(term.Descending);

                // Check if the sort key is a simple property name (NameNode) or self-reference (VariableNode "$")
                if (term.Expression is NameNode nameKey)
                {
                    string nameField = GetOrCreateNameField(nameKey.Value);
                    extractorExprs.Add($"static (el, ws) => {H}.NavigateProperty(el, {nameField}, ws)");
                }
                else if (term.Expression is VariableNode varKey && varKey.Name == "")
                {
                    // $  — sort by the element value itself
                    extractorExprs.Add("static (el, ws) => el");
                }
                else if (term.Expression is PathNode pathKey)
                {
                    // Compound path like Description.Colour — emit inline
                    int lambdaIdx = _lambdaCounter++;
                    string elParam = $"el_{lambdaIdx}";
                    string wsParam = $"ws_{lambdaIdx}";
                    string innerIndent = indent + "    ";
                    StringBuilder lambdaBody = new();

                    string bodyResult = EmitExpression(lambdaBody, pathKey, innerIndent, elParam, wsParam);
                    if (lambdaBody.Length == 0)
                    {
                        extractorExprs.Add($"static ({elParam}, {wsParam}) => {bodyResult}");
                    }
                    else
                    {
                        extractorExprs.Add($"({elParam}, {wsParam}) =>\n{indent}{{\n{lambdaBody}{innerIndent}return {bodyResult};\n{indent}}}");
                    }
                }
                else
                {
                    // Computed expression — emit as lambda
                    int lambdaIdx = _lambdaCounter++;
                    string elParam = $"el_{lambdaIdx}";
                    string wsParam = $"ws_{lambdaIdx}";
                    string innerIndent = indent + "    ";
                    StringBuilder lambdaBody = new();

                    string bodyResult = EmitExpression(lambdaBody, term.Expression, innerIndent, elParam, wsParam);
                    if (lambdaBody.Length == 0)
                    {
                        extractorExprs.Add($"({elParam}, {wsParam}) => {bodyResult}");
                    }
                    else
                    {
                        extractorExprs.Add($"({elParam}, {wsParam}) =>\n{indent}{{\n{lambdaBody}{innerIndent}return {bodyResult};\n{indent}}}");
                    }
                }
            }

            string extractorsArray = string.Join(",\n" + indent + "    ", extractorExprs);
            string descendingArray = string.Join(", ", descendingValues.ConvertAll(d => d ? "true" : "false"));

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.SortByKeys({currentVar},");
            L(sb, indent, $"    new Func<JsonElement, JsonWorkspace, JsonElement>[] {{ {extractorsArray} }},");
            L(sb, indent, $"    new bool[] {{ {descendingArray} }}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Tries to fuse a property chain segment with a following computed step into a single
        /// helper call that uses <see cref="ElementBuffer"/> for the intermediate chain result,
        /// avoiding the <see cref="JsonElement.CreateArrayBuilder"/> overhead of <c>NavigatePropertyChain</c>.
        /// </summary>
        /// <returns>The result variable name, or <c>null</c> if the pattern can't be fused.</returns>
        private string? TryEmitFusedChainStep(
            StringBuilder sb, List<JsonataNode> steps, int chainStart, int chainEnd,
            JsonataNode computedStep, string indent, string currentVar, string wsVar)
        {
            // Build the chain field reference
            string chainField = GetOrCreateChainField(steps, chainStart, chainEnd);

            // Arithmetic computed step → MapChainDouble
            if (computedStep is not ArrayConstructorNode
                && computedStep is not ObjectConstructorNode
                && GetArithmeticBody(computedStep) is BinaryNode arithBody
                && !IsConstantNumericExpression(arithBody))
            {
                int lambdaIdx = _lambdaCounter++;
                string elParam = $"el_{lambdaIdx}";
                string wsParam = $"ws_{lambdaIdx}";
                string innerIndent = indent + "    ";
                StringBuilder lambdaBody = new();

                string doubleResult = EmitArithmeticAsDouble(lambdaBody, arithBody, innerIndent, elParam, wsParam);

                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.MapChainDouble({currentVar}, {chainField}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
                L(sb, indent, "{");
                sb.Append(lambdaBody);
                L(sb, innerIndent, $"return {doubleResult};");
                L(sb, indent, $"}}, {wsVar});");
                return v;
            }

            // Object constructor computed step — fused chain + per-element object construction.
            // For NameNode keys (groupby semantics): use FusedChainGroupByPerElement helper.
            // For StringNode keys (literal keys): use ApplyChainStep with EmitObjectConstructor lambda.
            if (computedStep is ObjectConstructorNode objCtor)
            {
                // Single-pair with both simple name nodes: use specialized fused groupby helper
                if (objCtor.Pairs.Count == 1
                    && TryGetSimpleNameNode(objCtor.Pairs[0].Key, out string? keyName)
                    && TryGetSimpleNameNode(objCtor.Pairs[0].Value, out string? valName))
                {
                    string keyField = GetOrCreateNameField(keyName);
                    string valField = GetOrCreateNameField(valName);
                    string v = NextVar();
                    L(sb, indent, $"var {v} = {H}.FusedChainGroupByPerElement({currentVar}, {chainField}, {keyField}, {valField}, {wsVar});");
                    return v;
                }

                // All-StringNode keys: use ApplyChainStep with per-element object construction.
                // EmitObjectConstructor handles StringNode keys correctly as literal property names.
                bool allStringKeys = true;
                foreach ((JsonataNode key, _) in objCtor.Pairs)
                {
                    if (key is not StringNode) { allStringKeys = false; break; }
                }

                if (allStringKeys)
                {
                    int lambdaIdx = _lambdaCounter++;
                    string elParam = $"el_{lambdaIdx}";
                    string wsParam = $"ws_{lambdaIdx}";
                    string innerIndent = indent + "    ";
                    StringBuilder lambdaBody = new();

                    string objResult = EmitObjectConstructor(lambdaBody, objCtor, innerIndent, elParam, wsParam);

                    string v = NextVar();
                    L(sb, indent, $"var {v} = {H}.ApplyChainStep({currentVar}, {chainField}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
                    L(sb, indent, "{");
                    sb.Append(lambdaBody);
                    L(sb, innerIndent, $"return {objResult};");
                    L(sb, indent, $"}}, {wsVar});");
                    return v;
                }

                // NameNode keys with multi-pair or complex values: can't fuse, skip
                return null;
            }

            // General computed step → ApplyChainStep
            {
                int lambdaIdx = _lambdaCounter++;
                string elParam = $"el_{lambdaIdx}";
                string wsParam = $"ws_{lambdaIdx}";
                string innerIndent = indent + "    ";
                StringBuilder lambdaBody = new();

                string bodyResult = EmitExpression(lambdaBody, computedStep, innerIndent, elParam, wsParam);

                // Array constructor steps use different helpers — don't fuse those
                if (computedStep is ArrayConstructorNode)
                {
                    return null;
                }

                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.ApplyChainStep({currentVar}, {chainField}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
                L(sb, indent, "{");
                sb.Append(lambdaBody);
                L(sb, innerIndent, $"return {bodyResult};");
                L(sb, indent, $"}}, {wsVar});");
                return v;
            }
        }

        private string EmitComputedStep(
            StringBuilder sb, JsonataNode step, string currentVar, string indent, string wsVar,
            bool followedByConsArray = false)
        {
            // Fusion: if the step body is pure arithmetic, use MapOverElementsDouble
            // to avoid per-element DoubleToElement/FixedJsonValueDocument overhead.
            // The doubles are written directly into the array builder via AddItem(double).
            if (!followedByConsArray && step is not ArrayConstructorNode
                && GetArithmeticBody(step) is BinaryNode arithBody
                && !IsConstantNumericExpression(arithBody))
            {
                int lambdaIdx = _lambdaCounter++;
                string elParam = $"el_{lambdaIdx}";
                string wsParam = $"ws_{lambdaIdx}";
                string innerIndent = indent + "    ";
                StringBuilder lambdaBody = new();

                string doubleResult = EmitArithmeticAsDouble(lambdaBody, arithBody, innerIndent, elParam, wsParam);

                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.MapOverElementsDouble({currentVar}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
                L(sb, indent, "{");
                sb.Append(lambdaBody);
                L(sb, innerIndent, $"return {doubleResult};");
                L(sb, indent, $"}}, {wsVar});");
                return v;
            }

            int lambdaIdx2 = _lambdaCounter++;
            string elParam2 = $"el_{lambdaIdx2}";
            string wsParam2 = $"ws_{lambdaIdx2}";
            string innerIndent2 = indent + "    ";
            StringBuilder lambdaBody2 = new();

            string bodyResult = EmitExpression(lambdaBody2, step, innerIndent2, elParam2, wsParam2);

            // Array constructor steps (e.g. .[expr,expr]) use NoFlatten to preserve array structure.
            // When followed by [] (either as a separate ConsArray step or absorbed as KeepArray on the node),
            // use ApplyStepCollectingResults to always produce a collection array (wrapping even single-element results).
            bool needsCollectionSemantics = followedByConsArray || step.KeepArray;
            string helperName = step is ArrayConstructorNode
                ? (needsCollectionSemantics ? "ApplyStepCollectingResults" : "ApplyStepOverElementsNoFlatten")
                : "ApplyStepOverElements";

            string v2 = NextVar();
            L(sb, indent, $"var {v2} = {H}.{helperName}({currentVar}, {Static}(JsonElement {elParam2}, JsonWorkspace {wsParam2}) =>");
            L(sb, indent, "{");
            sb.Append(lambdaBody2);
            L(sb, innerIndent2, $"return {bodyResult};");
            L(sb, indent, $"}}, {wsVar});");
            return v2;
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
            // Deep constant folding — evaluates the entire arithmetic subtree at codegen time.
            // IsConstantNumericExpression checks first (cheap), then evaluates.
            if (IsConstantNumericExpression(binary))
            {
                if (TryEvaluateConstant(binary, out double constResult))
                {
                    return EmitDoubleConstant(sb, constResult, indent, wsVar);
                }

                // Entirely constant but non-finite (e.g. 1/0 → Infinity, 0%0 → NaN).
                // Fall back to runtime which propagates these correctly (e.g. $string(1/0) → D3001).
                throw new FallbackException();
            }

            // Emit the entire arithmetic subtree as raw doubles (matching the runtime's
            // Sequence.FromDouble pattern), then materialize to JsonElement only at the boundary.
            string doubleVar = EmitArithmeticAsDouble(sb, binary, indent, dataVar, wsVar);
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.DoubleToElement({doubleVar}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emits an arithmetic binary node as a raw <c>double</c> variable, keeping
        /// the entire arithmetic chain in double-space without intermediate materialization.
        /// </summary>
        private string EmitArithmeticAsDouble(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar, string wsVar)
        {
            string lhsD = EmitArithmeticOperandAsDouble(sb, binary.Lhs, indent, dataVar, wsVar, isLeft: true);
            string rhsD = EmitArithmeticOperandAsDouble(sb, binary.Rhs, indent, dataVar, wsVar, isLeft: false);
            string v = NextVar();
            string method = binary.Operator switch
            {
                "+" => "ArithmeticAdd",
                "-" => "ArithmeticSubtract",
                "*" => "ArithmeticMultiply",
                "/" => "ArithmeticDivide",
                "%" => "ArithmeticModulo",
                _ => throw new FallbackException(),
            };

            L(sb, indent, $"double {v} = {H}.{method}({lhsD}, {rhsD});");
            return v;
        }

        /// <summary>
        /// Emits an operand of an arithmetic expression as a raw <c>double</c>.
        /// For arithmetic sub-expressions, recurses in double-space.
        /// For number literals, emits a <c>double</c> constant.
        /// For everything else (property navigation, function calls, etc.),
        /// emits as <see cref="JsonElement"/> then extracts the double.
        /// </summary>
        private string EmitArithmeticOperandAsDouble(
            StringBuilder sb, JsonataNode node, string indent, string dataVar, string wsVar,
            bool isLeft)
        {
            switch (node)
            {
                case NumberNode num:
                    string dv = NextVar();
                    string literal = num.Value.ToString("R", CultureInfo.InvariantCulture);
                    L(sb, indent, $"double {dv} = {literal};");
                    return dv;

                case UnaryNode { Operator: "-" } unary:
                    string inner = EmitArithmeticOperandAsDouble(sb, unary.Expression, indent, dataVar, wsVar, isLeft);
                    string neg = NextVar();
                    L(sb, indent, $"double {neg} = -{inner};");
                    return neg;

                case BinaryNode { Operator: "+" or "-" or "*" or "/" or "%" } binary:
                    return EmitArithmeticAsDouble(sb, binary, indent, dataVar, wsVar);

                default:
                    // Non-arithmetic leaf: emit as element, extract double
                    string elem = EmitExpression(sb, node, indent, dataVar, wsVar);
                    string d = NextVar();
                    string method = isLeft ? "ToArithmeticDoubleLeft" : "ToArithmeticDoubleRight";
                    L(sb, indent, $"double {d} = {H}.{method}({elem});");
                    return d;
            }
        }

        /// <summary>
        /// Recursively evaluates a constant numeric expression at code generation time.
        /// Returns <see langword="true"/> if the entire subtree is a compile-time constant.
        /// </summary>
        private static bool TryEvaluateConstant(JsonataNode node, out double value)
        {
            switch (node)
            {
                case NumberNode num:
                    value = num.Value;
                    return true;

                case UnaryNode { Operator: "-" } unary:
                    if (TryEvaluateConstant(unary.Expression, out double inner))
                    {
                        value = -inner;
                        return true;
                    }

                    break;

                case BinaryNode { Operator: "+" or "-" or "*" or "/" or "%" } binary:
                    if (TryEvaluateConstant(binary.Lhs, out double lv) &&
                        TryEvaluateConstant(binary.Rhs, out double rv))
                    {
                        value = binary.Operator switch
                        {
                            "+" => lv + rv,
                            "-" => lv - rv,
                            "*" => lv * rv,
                            "/" => lv / rv,
                            "%" => lv % rv,
                            _ => double.NaN,
                        };

                        // Non-finite results (1/0, 0%0) — return true but the caller
                        // must check and handle non-finite values appropriately.
                        return !double.IsNaN(value) && !double.IsInfinity(value);
                    }

                    break;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Returns <see langword="true"/> if the entire subtree consists only of
        /// number literals, unary negation, and arithmetic operators — no data access.
        /// </summary>
        private static bool IsConstantNumericExpression(JsonataNode node)
        {
            return node switch
            {
                NumberNode => true,
                UnaryNode { Operator: "-" } u => IsConstantNumericExpression(u.Expression),
                BinaryNode { Operator: "+" or "-" or "*" or "/" or "%" } b
                    => IsConstantNumericExpression(b.Lhs) && IsConstantNumericExpression(b.Rhs),
                _ => false,
            };
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

        /// <summary>
        /// Tries to emit an expression that returns <see langword="bool"/> directly,
        /// avoiding the <c>JsonElement</c> → <c>IsTruthy</c> roundtrip. Currently handles
        /// ordered comparisons (<c>&gt;</c>, <c>&gt;=</c>, <c>&lt;</c>, <c>&lt;=</c>) where
        /// one operand is a numeric constant.
        /// </summary>
        /// <returns>A <c>bool</c> variable name, or <c>null</c> if the pattern doesn't match.</returns>
        private string? TryEmitExpressionAsBool(
            StringBuilder sb, JsonataNode expr, string indent, string dataVar, string wsVar)
        {
            if (expr is not BinaryNode binary)
            {
                return null;
            }

            // Map operator → fused helper name (element on left, constant on right)
            string? helperName = binary.Operator switch
            {
                ">" => "CompareNumberGT",
                ">=" => "CompareNumberGTE",
                "<" => "CompareNumberLT",
                "<=" => "CompareNumberLTE",
                _ => null,
            };

            if (helperName is null)
            {
                return null;
            }

            // Constant on the right: element > 30
            if (binary.Rhs is NumberNode rightNum)
            {
                string lhs = EmitExpression(sb, binary.Lhs, indent, dataVar, wsVar);
                string literal = rightNum.Value.ToString("R", CultureInfo.InvariantCulture);
                string v = NextVar();
                L(sb, indent, $"bool {v} = {H}.{helperName}({lhs}, {literal});");
                return v;
            }

            // Constant on the left: 30 > element → element < 30
            if (binary.Lhs is NumberNode leftNum)
            {
                string flipped = binary.Operator switch
                {
                    ">" => "CompareNumberLT",
                    ">=" => "CompareNumberLTE",
                    "<" => "CompareNumberGT",
                    "<=" => "CompareNumberGTE",
                    _ => null!,
                };
                string rhs = EmitExpression(sb, binary.Rhs, indent, dataVar, wsVar);
                string literal = leftNum.Value.ToString("R", CultureInfo.InvariantCulture);
                string v = NextVar();
                L(sb, indent, $"bool {v} = {H}.{flipped}({rhs}, {literal});");
                return v;
            }

            return null;
        }

        private string EmitStringConcat(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar, string wsVar)
        {
            // Flatten chain: A & B & C & D → collect all operands
            // AutoMapPropField is non-null when the operand is a single-property navigation
            // from a source that might be an array. In that case, Var is the source variable
            // and AppendAutoMap fuses the navigation into the concat buffer directly.
            List<(string Var, bool IsLiteral, string? AutoMapPropField)> operands = new();
            FlattenConcatOperands(sb, binary, indent, dataVar, wsVar, operands);

            bool hasLiterals = false;
            bool hasAutoMaps = false;
            for (int i = 0; i < operands.Count; i++)
            {
                if (operands[i].IsLiteral)
                {
                    hasLiterals = true;
                }

                if (operands[i].AutoMapPropField != null)
                {
                    hasAutoMaps = true;
                }
            }

            if (!hasLiterals && !hasAutoMaps)
            {
                // No literals or auto-maps — use existing helper calls
                if (operands.Count == 2)
                {
                    string v = NextVar();
                    L(sb, indent, $"var {v} = {H}.StringConcat({operands[0].Var}, {operands[1].Var}, {wsVar});");
                    return v;
                }

                string args = string.Join(", ", operands.ConvertAll(o => o.Var));
                string result = NextVar();

                if (operands.Count <= 5)
                {
                    string method = $"StringConcat{operands.Count}";
                    L(sb, indent, $"var {result} = {H}.{method}({args}, {wsVar});");
                }
                else
                {
                    L(sb, indent, $"var {result} = {H}.StringConcatMany({wsVar}, {args});");
                }

                return result;
            }

            // Has literals or auto-maps — emit ConcatBuilder
            string cbVar = NextVar();
            string resultVar = NextVar();
            L(sb, indent, $"var {cbVar} = {H}.BeginConcat(stackalloc byte[256]);");
            L(sb, indent, $"JsonElement {resultVar};");
            L(sb, indent, "try");
            L(sb, indent, "{");
            string innerIndent = indent + "    ";

            for (int i = 0; i < operands.Count; i++)
            {
                if (operands[i].IsLiteral)
                {
                    L(sb, innerIndent, $"{cbVar}.AppendLiteral({operands[i].Var});");
                }
                else if (operands[i].AutoMapPropField != null)
                {
                    L(sb, innerIndent, $"{cbVar}.AppendAutoMap({operands[i].Var}, {operands[i].AutoMapPropField});");
                }
                else
                {
                    L(sb, innerIndent, $"{cbVar}.AppendElement({operands[i].Var});");
                }
            }

            L(sb, innerIndent, $"{resultVar} = {cbVar}.Complete({wsVar});");
            L(sb, indent, "}");
            L(sb, indent, "finally");
            L(sb, indent, "{");
            L(sb, innerIndent, $"{cbVar}.Dispose();");
            L(sb, indent, "}");
            return resultVar;
        }

        private void FlattenConcatOperands(
            StringBuilder sb, BinaryNode binary, string indent, string dataVar,
            string wsVar, List<(string Var, bool IsLiteral, string? AutoMapPropField)> operands)
        {
            // Recurse into left & operator chains
            if (binary.Lhs is BinaryNode { Operator: "&" } leftConcat)
            {
                FlattenConcatOperands(sb, leftConcat, indent, dataVar, wsVar, operands);
            }
            else if (binary.Lhs is StringNode lhsStr)
            {
                operands.Add(($"\"{EscapeStringLiteral(lhsStr.Value)}\"u8", true, null));
            }
            else
            {
                EmitConcatOperand(sb, binary.Lhs, indent, dataVar, wsVar, operands);
            }

            // Right side is usually a leaf (left-associative parsing)
            if (binary.Rhs is BinaryNode { Operator: "&" } rightConcat)
            {
                FlattenConcatOperands(sb, rightConcat, indent, dataVar, wsVar, operands);
            }
            else if (binary.Rhs is StringNode rhsStr)
            {
                operands.Add(($"\"{EscapeStringLiteral(rhsStr.Value)}\"u8", true, null));
            }
            else
            {
                EmitConcatOperand(sb, binary.Rhs, indent, dataVar, wsVar, operands);
            }
        }

        /// <summary>
        /// Emits a concat operand, detecting auto-map candidates.
        /// A 2+ step simple property chain like Employee.FirstName can be fused:
        /// evaluate all steps except the last, then use <c>AppendAutoMap</c> in the
        /// concat to handle the last step (avoiding intermediate array documents
        /// when the penultimate step result is an array).
        /// </summary>
        private void EmitConcatOperand(
            StringBuilder sb, JsonataNode operand, string indent, string dataVar,
            string wsVar, List<(string Var, bool IsLiteral, string? AutoMapPropField)> operands)
        {
            // Detect auto-map candidate: PathNode with 2+ simple name steps, no annotations
            if (operand is PathNode autoMapPath
                && autoMapPath.Steps.Count >= 2
                && IsSimplePropertyChain(autoMapPath.Steps)
                && autoMapPath.Annotations?.Group is null
                && !autoMapPath.KeepSingletonArray
                && !autoMapPath.KeepArray
                && !autoMapPath.Steps.Exists(s => s.KeepArray))
            {
                int lastIdx = autoMapPath.Steps.Count - 1;
                string lastStepName = ((NameNode)autoMapPath.Steps[lastIdx]).Value;
                string propField = GetOrCreateNameField(lastStepName);

                // Evaluate all steps except the last (the prefix)
                string sourceVar;
                if (lastIdx == 1)
                {
                    // 2-step path: evaluate only the first step
                    string firstName = ((NameNode)autoMapPath.Steps[0]).Value;
                    var cacheKey = (dataVar, firstName);
                    if (_propertyStepCache.TryGetValue(cacheKey, out string? cached))
                    {
                        sourceVar = cached;
                    }
                    else
                    {
                        string escapedName = EscapeStringLiteral(firstName);
                        string nameField = GetOrCreateNameField(firstName);
                        sourceVar = NextVar();
                        L(sb, indent, $"JsonElement {sourceVar};");
                        L(sb, indent, $"if ({dataVar}.ValueKind == JsonValueKind.Object && {dataVar}.TryGetProperty(\"{escapedName}\"u8, out {sourceVar}))");
                        L(sb, indent, "{");
                        L(sb, indent, "}");
                        L(sb, indent, "else");
                        L(sb, indent, "{");
                        L(sb, indent, $"    {sourceVar} = {H}.NavigateProperty({dataVar}, {nameField}, {wsVar});");
                        L(sb, indent, "}");
                        L(sb, indent, "");
                        _propertyStepCache[cacheKey] = sourceVar;
                    }
                }
                else
                {
                    // 3+ step path: evaluate prefix chain (all but last step)
                    sourceVar = EmitInlinePropertyChain(sb, autoMapPath.Steps, 0, lastIdx, indent, dataVar, wsVar);
                }

                operands.Add((sourceVar, false, propField));
                return;
            }

            // Default: emit the full expression
            operands.Add((EmitExpression(sb, operand, indent, dataVar, wsVar), false, null));
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
                        // Small constant ranges: emit inline CVB array
                        var elVars = new string[(int)count];
                        for (int i = 0; i < (int)count; i++)
                        {
                            elVars[i] = EmitDoubleConstant(sb, start + i, indent, wsVar);
                        }

                        return EmitCreateArrayCVB(sb, elVars, indent, wsVar);
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

            if (_knownObjectDataVar != null && dataVar == _knownObjectDataVar)
            {
                // Inline TryGetProperty — skips NavigateProperty function call + Array branch
                string tmp = NextVar();
                L(sb, indent, $"var {v} = {dataVar}.ValueKind == JsonValueKind.Object && {dataVar}.TryGetProperty((ReadOnlySpan<byte>){nameField}, out var {tmp}) ? {tmp} : default;");
            }
            else
            {
                L(sb, indent, $"var {v} = {H}.NavigateProperty({dataVar}, {nameField}, {wsVar});");
            }

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
                L(sb, indent, $"var {v} = (JsonElement)JsonElement.CreateArrayBuilder({wsVar}, 0).RootElement;");
                return v;
            }

            // Fused array-of-objects: [path.path.path.{StringKey: expr, ...}]
            // Detect single-expression ArrayConstructor wrapping a PathNode that ends with
            // an ObjectConstructorNode (all-StringNode keys) preceded by 2+ NameNode chain steps.
            // Emits FusedChainBuildArray which builds objects directly into the array document.
            if (arr.Expressions.Count == 1
                && arr.Expressions[0] is PathNode path
                && path.Steps.Count >= 3
                && path.Steps[path.Steps.Count - 1] is ObjectConstructorNode objCtor
                && !HasComplexAnnotations(path.Steps[path.Steps.Count - 1]))
            {
                // Check: all preceding steps are plain NameNodes, last step is ObjectConstructor with all StringNode keys
                bool allNameSteps = true;
                for (int s = 0; s < path.Steps.Count - 1; s++)
                {
                    if (path.Steps[s] is not NameNode || HasComplexAnnotations(path.Steps[s]))
                    {
                        allNameSteps = false;
                        break;
                    }
                }

                bool allStringKeys = true;
                foreach ((JsonataNode key, _) in objCtor.Pairs)
                {
                    if (key is not StringNode) { allStringKeys = false; break; }
                }

                if (allNameSteps && allStringKeys)
                {
                    return EmitFusedArrayOfObjects(sb, path, objCtor, indent, dataVar, wsVar);
                }
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

            int n = arr.Expressions.Count;

            // If any expression is NOT an array constructor, we need flatten semantics
            bool needsFlatten = isArrayCtorMask != 0 && isArrayCtorMask != ((1L << n) - 1);

            // Also need flatten when no array constructors but some expressions might produce arrays
            // In JSONata, [path.to.array] flattens the array result
            if (isArrayCtorMask == 0)
            {
                // All expressions are non-array-constructor: all array results should be flattened
                needsFlatten = true;
            }

            if (needsFlatten)
            {
                // Flatten case still needs the runtime helper (complex per-element logic),
                // but we use per-arity overloads to avoid the array allocation.
                return EmitCreateArrayWithFlatten(sb, elemVars, isArrayCtorMask, indent, wsVar);
            }
            else
            {
                // No flatten needed: use CVB pattern — forward-only AddItem via static callback.
                return EmitCreateArrayCVB(sb, elemVars, indent, wsVar);
            }
        }

        /// <summary>
        /// Emits a CVB (ComplexValueBuilder) array construction using
        /// <c>JsonElement.CreateBuilder</c> with an <c>ArrayBuilder.Build</c> callback.
        /// Avoids <c>new JsonElement[]</c> heap allocation and Mutable.AddItem version tracking.
        /// </summary>
        private string EmitCreateArrayCVB(
            StringBuilder sb, string[] elemVars, string indent, string wsVar)
        {
            int n = elemVars.Length;

            string ctxExpr = n == 1
                ? $"ValueTuple.Create({elemVars[0]})"
                : $"({string.Join(", ", elemVars)})";

            string ctxType = n == 1
                ? "ValueTuple<JsonElement>"
                : $"({string.Join(", ", Enumerable.Range(0, n).Select(_ => "JsonElement"))})";

            string docVar = NextVar();
            L(sb, indent, $"var {docVar} = JsonElement.CreateBuilder({wsVar}, {ctxExpr}, static (in {ctxType} __ctx, ref JsonElement.ArrayBuilder __b) =>");
            L(sb, indent, "{");

            for (int i = 0; i < n; i++)
            {
                string itemRef = n == 1 ? "__ctx.Item1" : $"__ctx.Item{i + 1}";
                L(sb, indent, $"    if ({itemRef}.ValueKind != JsonValueKind.Undefined) __b.AddItem({itemRef});");
            }

            L(sb, indent, $"}}, {n});");

            string v = NextVar();
            L(sb, indent, $"var {v} = (JsonElement){docVar}.RootElement;");
            return v;
        }

        /// <summary>
        /// Emits fused array-of-objects construction: navigates a property chain,
        /// then builds objects directly into the array document via
        /// <c>AddItem&lt;TContext&gt;(ctx, ObjectBuilder.Build, count)</c>.
        /// Eliminates intermediate object document allocations.
        /// Pattern: <c>[path.path.path.{StringKey: expr, ...}]</c>.
        /// </summary>
        private string EmitFusedArrayOfObjects(
            StringBuilder sb, PathNode path, ObjectConstructorNode objCtor,
            string indent, string dataVar, string wsVar)
        {
            // Build chain field for prefix NameNode steps
            int chainLen = path.Steps.Count - 1;
            string[] chainNames = new string[chainLen];
            for (int i = 0; i < chainLen; i++)
            {
                chainNames[i] = ((NameNode)path.Steps[i]).Value;
            }

            string chainField = CreatePathField(chainNames);

            // Try fully-fused path: evaluate values inside ObjectBuilder callback.
            // This eliminates DoubleToElement allocations for arithmetic and avoids
            // intermediate ValueTuple + delegate overhead.
            int n = objCtor.Pairs.Count;
            string[] keyLiterals = new string[n];
            string?[] simpleProps = new string?[n];
            (string left, string right, string csOp, string helper)?[] arithmeticPairs = new (string, string, string, string)?[n];
            bool allSimple = true;

            for (int i = 0; i < n; i++)
            {
                string keyValue = ((StringNode)objCtor.Pairs[i].Key).Value;
                keyLiterals[i] = $"\"{EscapeStringLiteral(keyValue)}\"u8";

                JsonataNode valNode = objCtor.Pairs[i].Value;

                if (TryGetSinglePropertyName(valNode, out string? propName))
                {
                    simpleProps[i] = propName;
                }
                else if (valNode is BinaryNode bin
                    && IsArithmeticOp(bin.Operator, out string? csOp, out string? helperName)
                    && TryGetSinglePropertyName(bin.Lhs, out string? leftProp)
                    && TryGetSinglePropertyName(bin.Rhs, out string? rightProp))
                {
                    arithmeticPairs[i] = (leftProp!, rightProp!, csOp!, helperName!);
                }
                else
                {
                    allSimple = false;
                    break;
                }
            }

            if (allSimple)
            {
                return EmitFusedInlineObjectBuilder(sb, chainField, keyLiterals, simpleProps, arithmeticPairs, n, indent, dataVar, wsVar);
            }

            // Fallback: evaluate values outside callback, pass via ValueTuple.
            return EmitFusedTupleObjectBuilder(sb, objCtor, chainField, keyLiterals, n, indent, dataVar, wsVar);
        }

        /// <summary>
        /// Extracts the property name from a node that is a simple single-step property access:
        /// either a bare <see cref="NameNode"/> or a <see cref="PathNode"/> wrapping a single
        /// <see cref="NameNode"/> step with no annotations.
        /// </summary>
        private static bool TryGetSinglePropertyName(JsonataNode node, out string? name)
        {
            if (node is NameNode nn && !HasComplexAnnotations(nn) && !HasStages(nn))
            {
                name = nn.Value;
                return true;
            }

            if (node is PathNode pn && pn.Steps.Count == 1
                && pn.Steps[0] is NameNode pnn
                && !HasComplexAnnotations(pn) && !HasStages(pn)
                && !HasComplexAnnotations(pnn) && !HasStages(pnn)
                && !pn.KeepArray && !pn.KeepSingletonArray)
            {
                name = pnn.Value;
                return true;
            }

            name = null;
            return false;
        }

        /// <summary>
        /// Maps a JSONata arithmetic operator to its C# equivalent and helper name.
        /// </summary>
        private static bool IsArithmeticOp(string op, out string? csOp, out string? helperName)
        {
            switch (op)
            {
                case "+": csOp = "+"; helperName = "Add"; return true;
                case "-": csOp = "-"; helperName = "Subtract"; return true;
                case "*": csOp = "*"; helperName = "Multiply"; return true;
                case "/": csOp = "/"; helperName = "Divide"; return true;
                case "%": csOp = "%"; helperName = "Modulo"; return true;
                default: csOp = null; helperName = null; return false;
            }
        }

        /// <summary>
        /// Emits the fully-fused path: values are evaluated INSIDE the ObjectBuilder
        /// callback. Context is just the per-element JsonElement. Arithmetic results
        /// use <c>AddProperty(key, double)</c> directly — no <c>DoubleToElement</c>
        /// allocation.
        /// </summary>
        private string EmitFusedInlineObjectBuilder(
            StringBuilder sb, string chainField,
            string[] keyLiterals, string?[] simpleProps,
            (string left, string right, string csOp, string helper)?[] arithmeticPairs,
            int n, string indent, string dataVar, string wsVar)
        {
            int lambdaIdx = _lambdaCounter++;
            string elParam = $"__el_{lambdaIdx}";
            string wsParam = $"__ws_{lambdaIdx}";
            string arrParam = $"__arr_{lambdaIdx}";
            string innerIndent = indent + "    ";
            string bodyIndent = innerIndent + "    ";
            string propIndent = bodyIndent + "    ";

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.FusedChainBuildArray({dataVar}, {chainField}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}, JsonElement.Mutable {arrParam}) =>");
            L(sb, indent, "{");

            // Pass element as context; evaluate everything inside the callback
            L(sb, innerIndent, $"{arrParam}.AddItem({elParam}, {Static}(in JsonElement __ctx, ref JsonElement.ObjectBuilder __b) =>");
            L(sb, innerIndent, "{");
            L(sb, bodyIndent, "if (__ctx.ValueKind == JsonValueKind.Object)");
            L(sb, bodyIndent, "{");

            for (int i = 0; i < n; i++)
            {
                if (simpleProps[i] != null)
                {
                    // Simple property: TryGetProperty → AddProperty(key, element)
                    string nameField = GetOrCreateNameField(simpleProps[i]!);
                    string tmpVar = NextVar();
                    L(sb, propIndent, $"if (__ctx.TryGetProperty((ReadOnlySpan<byte>){nameField}, out var {tmpVar}))");
                    L(sb, propIndent, $"    __b.AddProperty({keyLiterals[i]}, {tmpVar});");
                }
                else if (arithmeticPairs[i] is var (left, right, csOp, helper))
                {
                    // Arithmetic: TryGetProperty × 2 → ArithmeticOp → AddProperty(key, double)
                    string leftField = GetOrCreateNameField(left);
                    string rightField = GetOrCreateNameField(right);
                    string lv = NextVar();
                    string rv = NextVar();
                    string ld = NextVar();
                    string rd = NextVar();
                    string res = NextVar();
                    L(sb, propIndent, $"if (__ctx.TryGetProperty((ReadOnlySpan<byte>){leftField}, out var {lv}) && {lv}.TryGetDouble(out double {ld})");
                    L(sb, propIndent, $"    && __ctx.TryGetProperty((ReadOnlySpan<byte>){rightField}, out var {rv}) && {rv}.TryGetDouble(out double {rd}))");
                    L(sb, propIndent, "{");
                    L(sb, propIndent, $"    double {res} = {H}.Arithmetic{helper}({ld}, {rd});");
                    L(sb, propIndent, $"    if (!double.IsNaN({res})) __b.AddProperty({keyLiterals[i]}, {res});");
                    L(sb, propIndent, "}");
                }
            }

            L(sb, bodyIndent, "}");
            L(sb, innerIndent, $"}}, {n});");
            L(sb, indent, $"}}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Fallback: evaluates values outside the ObjectBuilder callback, passes
        /// results via ValueTuple. Used when value expressions are too complex
        /// for the inline path.
        /// </summary>
        private string EmitFusedTupleObjectBuilder(
            StringBuilder sb, ObjectConstructorNode objCtor, string chainField,
            string[] keyLiterals, int n, string indent, string dataVar, string wsVar)
        {
            int lambdaIdx = _lambdaCounter++;
            string elParam = $"__el_{lambdaIdx}";
            string wsParam = $"__ws_{lambdaIdx}";
            string arrParam = $"__arr_{lambdaIdx}";
            string innerIndent = indent + "    ";

            StringBuilder body = new();
            string[] valueVars = new string[n];

            string? prevKnownObj = _knownObjectDataVar;
            _knownObjectDataVar = elParam;

            for (int i = 0; i < n; i++)
            {
                valueVars[i] = EmitExpression(body, objCtor.Pairs[i].Value, innerIndent, elParam, wsParam);
            }

            _knownObjectDataVar = prevKnownObj;

            string ctxExpr = n == 1
                ? $"ValueTuple.Create({valueVars[0]})"
                : $"({string.Join(", ", valueVars)})";
            string ctxType = n == 1
                ? "ValueTuple<JsonElement>"
                : $"({string.Join(", ", Enumerable.Range(0, n).Select(_ => "JsonElement"))})";

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.FusedChainBuildArray({dataVar}, {chainField}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}, JsonElement.Mutable {arrParam}) =>");
            L(sb, indent, "{");
            sb.Append(body);
            L(sb, innerIndent, $"{arrParam}.AddItem({ctxExpr}, {Static}(in {ctxType} __ctx, ref JsonElement.ObjectBuilder __b) =>");
            L(sb, innerIndent, "{");
            for (int i = 0; i < n; i++)
            {
                string itemRef = n == 1 ? "__ctx.Item1" : $"__ctx.Item{i + 1}";
                L(sb, innerIndent, $"    if ({itemRef}.ValueKind != JsonValueKind.Undefined) __b.AddProperty({keyLiterals[i]}, {itemRef});");
            }

            L(sb, innerIndent, $"}}, {n});");
            L(sb, indent, $"}}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Emits array construction with flatten semantics. For small known counts,
        /// uses per-arity overloads to avoid <c>new JsonElement[]</c> allocation.
        /// </summary>
        private string EmitCreateArrayWithFlatten(
            StringBuilder sb, string[] elemVars, long isArrayCtorMask, string indent, string wsVar)
        {
            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.CreateArrayWithFlatten(new JsonElement[] {{ {string.Join(", ", elemVars)} }}, {isArrayCtorMask}L, {wsVar});");
            return v;
        }

        private string EmitObjectConstructor(
            StringBuilder sb, ObjectConstructorNode obj, string indent, string dataVar, string wsVar)
        {
            foreach ((JsonataNode key, _) in obj.Pairs)
            {
                if (key is not StringNode and not NameNode)
                {
                    throw new FallbackException();
                }
            }

            // Emit value expressions first (before building the object)
            string[] keyLiterals = new string[obj.Pairs.Count];
            string[] valueVars = new string[obj.Pairs.Count];

            for (int i = 0; i < obj.Pairs.Count; i++)
            {
                string keyValue = obj.Pairs[i].Key is StringNode s ? s.Value
                    : ((NameNode)obj.Pairs[i].Key).Value;
                keyLiterals[i] = $"\"{EscapeStringLiteral(keyValue)}\"u8";
                valueVars[i] = EmitExpression(sb, obj.Pairs[i].Value, indent, dataVar, wsVar);
            }

            int n = obj.Pairs.Count;

            if (n == 0)
            {
                // Empty object: CreateObjectBuilder is optimal (no properties to add).
                string docVar = NextVar();
                L(sb, indent, $"var {docVar} = (JsonElement)JsonElement.CreateObjectBuilder({wsVar}, 0).RootElement;");
                return docVar;
            }

            // Use CVB (ComplexValueBuilder) pattern: CreateBuilder with a static callback
            // writes properties forward-only into the metadata buffer, avoiding the
            // version-tracking and property-name-scan overhead of SetProperty.

            // Build a value tuple of computed values for the callback context.
            // For n==1 we must use ValueTuple.Create() since (x) is not a tuple literal.
            string ctxExpr = n == 1
                ? $"ValueTuple.Create({valueVars[0]})"
                : $"({string.Join(", ", valueVars)})";

            // Build the tuple type for the lambda parameter
            string ctxType = n == 1
                ? $"ValueTuple<JsonElement>"
                : $"({string.Join(", ", Enumerable.Range(0, n).Select(i => "JsonElement"))})";

            string builderVar = NextVar();
            L(sb, indent, $"var {builderVar} = JsonElement.CreateBuilder({wsVar}, {ctxExpr}, static (in {ctxType} __ctx, ref JsonElement.ObjectBuilder __b) =>");
            L(sb, indent, "{");

            for (int i = 0; i < n; i++)
            {
                string itemRef = n == 1 ? "__ctx.Item1" : $"__ctx.Item{i + 1}";
                L(sb, indent, $"    if ({itemRef}.ValueKind != JsonValueKind.Undefined) __b.AddProperty({keyLiterals[i]}, {itemRef});");
            }

            L(sb, indent, $"}}, {n});");

            string v = NextVar();
            L(sb, indent, $"var {v} = (JsonElement){builderVar}.RootElement;");
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
                "sum" => TryEmitSumDoubleFusion(sb, func, indent, dataVar, wsVar)
                         ?? EmitBuiltinUnaryOrError(sb, func, indent, dataVar, wsVar, "Sum", "sum"),
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
                "single" => EmitBuiltinSingle(sb, func, indent, dataVar, wsVar),
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
        /// Tries to fuse <c>$sum(path.arithmetic)</c> into <c>SumOverElementsDouble</c>,
        /// avoiding the per-element <c>DoubleToElement</c> + intermediate array + <c>GetDouble</c>
        /// roundtrip. The lambda produces a raw double per element, and only one
        /// <c>NumberFromDouble</c> materialises the final sum.
        /// </summary>
        /// <returns>The result variable name, or <c>null</c> if the pattern doesn't match.</returns>
        private string? TryEmitSumDoubleFusion(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar, string wsVar)
        {
            if (func.Arguments.Count != 1)
            {
                return null;
            }

            // Unwrap single-step path wrapper to get the real path.
            JsonataNode arg = func.Arguments[0];
            if (arg is PathNode outerPath && outerPath.Steps.Count == 1)
            {
                arg = outerPath.Steps[0];
            }

            if (arg is not PathNode path || path.Steps.Count < 2)
            {
                return null;
            }

            // The last step must be a block containing arithmetic.
            JsonataNode lastStep = path.Steps[^1];
            BinaryNode? arithmetic = GetArithmeticBody(lastStep);
            if (arithmetic is null)
            {
                return null;
            }

            // Emit the prefix path (all steps except the last computed step).
            PathNode prefixPath = new();
            for (int i = 0; i < path.Steps.Count - 1; i++)
            {
                prefixPath.Steps.Add(path.Steps[i]);
            }

            string inputVar = EmitPath(sb, prefixPath, indent, dataVar, wsVar);

            // Emit the fused SumOverElementsDouble call with a double-returning lambda.
            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "    ";
            StringBuilder lambdaBody = new();

            string doubleResult = EmitArithmeticAsDouble(lambdaBody, arithmetic, innerIndent, elParam, wsParam);

            string v = NextVar();
            L(sb, indent, $"var {v} = {H}.SumOverElementsDouble({inputVar}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            L(sb, indent, "{");
            sb.Append(lambdaBody);
            L(sb, innerIndent, $"return {doubleResult};");
            L(sb, indent, $"}}, {wsVar});");
            return v;
        }

        /// <summary>
        /// Extracts the arithmetic <see cref="BinaryNode"/> from a computed step,
        /// unwrapping a single-expression <see cref="BlockNode"/> if present.
        /// Returns <c>null</c> if the step is not a simple arithmetic expression.
        /// </summary>
        private static BinaryNode? GetArithmeticBody(JsonataNode step)
        {
            // Unwrap block containing a single expression: (Price * Quantity)
            if (step is BlockNode block && block.Expressions.Count == 1)
            {
                step = block.Expressions[0];
            }

            if (step is BinaryNode binary && binary.Operator is "+" or "-" or "*" or "/" or "%")
            {
                return binary;
            }

            return null;
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

            // Fused pattern: $join([elem1, elem2, ...], "separator")
            // Avoids intermediate array document construction and StringElement allocation.
            if (func.Arguments[0] is ArrayConstructorNode arr && arr.Expressions.Count > 0)
            {
                string sepUtf8;
                if (func.Arguments.Count >= 2 && func.Arguments[1] is StringNode sepNode)
                {
                    sepUtf8 = $"\"{EscapeStringLiteral(sepNode.Value)}\"u8";
                }
                else if (func.Arguments.Count < 2)
                {
                    sepUtf8 = "default";
                }
                else
                {
                    // Non-constant separator — fall through to generic path
                    goto generic;
                }

                // Emit each element, then use JoinBuilder pattern (like ConcatBuilder — zero allocation)
                List<string> elemVars = new(arr.Expressions.Count);
                foreach (var item in arr.Expressions)
                {
                    elemVars.Add(EmitExpression(sb, item, indent, dataVar, wsVar));
                }

                string jb = NextVar();
                string v = NextVar();
                L(sb, indent, $"var {jb} = {H}.BeginJoin(stackalloc byte[256], {sepUtf8});");
                L(sb, indent, $"JsonElement {v};");
                L(sb, indent, "try");
                L(sb, indent, "{");
                foreach (string ev in elemVars)
                {
                    L(sb, indent, $"    {jb}.AppendElement({ev});");
                }

                L(sb, indent, $"    {v} = {jb}.Complete({wsVar});");
                L(sb, indent, "}");
                L(sb, indent, "finally");
                L(sb, indent, "{");
                L(sb, indent, $"    {jb}.Dispose();");
                L(sb, indent, "}");

                return v;
            }

            generic:
            string arg0 = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
            string arg1 = func.Arguments.Count >= 2
                ? EmitExpression(sb, func.Arguments[1], indent, dataVar, wsVar)
                : "default";

            string vg = NextVar();
            L(sb, indent, $"var {vg} = {H}.Join({arg0}, {arg1}, {wsVar});");
            return vg;
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
            // Handle $map/$filter with built-in function reference (e.g. $map(arr, $string))
            if (func.Arguments.Count >= 2 && func.Arguments[1] is VariableNode builtinRef
                && IsBuiltinFunctionName(builtinRef.Name))
            {
                return EmitHofWithBuiltinRef(sb, func, indent, dataVar, wsVar, helperName, returnsElement, builtinRef.Name);
            }

            if (func.Arguments.Count < 2 || func.Arguments[1] is not LambdaNode lambda)
            {
                throw new FallbackException();
            }

            if (lambda.Parameters.Count < 1)
            {
                throw new FallbackException();
            }

            bool hasIndex = lambda.Parameters.Count >= 2 && helperName == "MapElements";

            // Fusion: detect simple property chain input BEFORE evaluating it,
            // so we skip the intermediate NavigatePropertyChain when fusing.
            string? chainField = !hasIndex ? TryGetSimpleChainField(func.Arguments[0]) : null;
            string? fusedHelper = chainField != null
                ? helperName switch
                {
                    "MapElements" => "MapChainElements",
                    "FilterElements" => "FilterChainElements",
                    _ => null,
                }
                : null;

            // Only evaluate the input expression if we're NOT fusing
            // (fused helpers navigate the chain internally from dataVar).
            string inputVar = fusedHelper != null
                ? dataVar  // placeholder — won't be used in the fused call
                : EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);

            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";
            string innerIndent = indent + "    ";
            StringBuilder lambdaBody = new();

            string? idxParam = hasIndex ? $"idx_{lambdaIdx}" : null;

            string? savedVar = StashVariable(lambda.Parameters[0], elParam);
            string? savedIdx = hasIndex ? StashVariable(lambda.Parameters[1], idxParam!) : null;

            // Inside HOF body, nested lambdas (e.g. ApplyStage predicates) may reference
            // outer HOF parameters. Disable static lambdas to avoid CS8820.
            bool prevRootRef = _usesRootRef;
            _usesRootRef = true;

            // For filter predicates (returnsElement=false), try fused comparison that returns bool directly,
            // avoiding NumberFromDouble + BooleanElement + IsTruthy per element.
            string? boolResult = !returnsElement
                ? TryEmitExpressionAsBool(lambdaBody, lambda.Body, innerIndent, elParam, wsParam)
                : null;
            string bodyResult = boolResult ?? EmitExpression(lambdaBody, lambda.Body, innerIndent, elParam, wsParam);

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
            else if (fusedHelper != null)
            {
                L(sb, indent, $"var {v} = {H}.{fusedHelper}({dataVar}, {chainField}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            }
            else
            {
                L(sb, indent, $"var {v} = {H}.{helperName}({inputVar}, {Static}(JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
            }

            L(sb, indent, "{");
            sb.Append(lambdaBody);

            if (boolResult != null)
            {
                L(sb, innerIndent, $"return {boolResult};");
            }
            else if (returnsElement)
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

        /// <summary>
        /// Emits a HOF call where the callback is a built-in function reference.
        /// E.g., $map([1,2,3], $string) or $filter([0,1,2], $boolean).
        /// </summary>
        private string EmitHofWithBuiltinRef(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar,
            string wsVar, string helperName, bool returnsElement, string builtinName)
        {
            string inputVar = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);

            // Map the built-in name to the helper call to apply per-element
            string? perElementCall = builtinName switch
            {
                "string" => "{H}.String({el}, {ws})",
                "number" => "{H}.Number({el}, {ws})",
                "boolean" => "{H}.BooleanElement({H}.IsTruthy({el}))",
                "not" => "{H}.BooleanElement(!{H}.IsTruthy({el}))",
                "uppercase" => "{H}.Uppercase({el}, {ws})",
                "lowercase" => "{H}.Lowercase({el}, {ws})",
                "trim" => "{H}.Trim({el}, {ws})",
                "length" => "{H}.Length({el}, {ws})",
                "type" => "{H}.Type({el}, {ws})",
                "exists" => "{H}.Exists({el}, {ws})",
                "abs" => "{H}.Abs({el}, {ws})",
                "floor" => "{H}.Floor({el}, {ws})",
                "ceil" => "{H}.Ceil({el}, {ws})",
                "sqrt" => "{H}.Sqrt({el}, {ws})",
                "sum" => "{H}.Sum({el}, {ws})",
                "count" => "{H}.Count({el}, {ws})",
                "max" => "{H}.Max({el}, {ws})",
                "min" => "{H}.Min({el}, {ws})",
                "average" => "{H}.Average({el}, {ws})",
                "reverse" => "{H}.Reverse({el}, {ws})",
                "shuffle" => "{H}.Shuffle({el}, {ws})",
                "sort" => "{H}.SortDefault({el}, {ws})",
                "keys" => "{H}.Keys({el}, {ws})",
                "values" => "{H}.Values({el}, {ws})",
                "spread" => "{H}.Spread({el}, {ws})",
                _ => null,
            };

            if (perElementCall is null)
            {
                throw new FallbackException();
            }

            int lambdaIdx = _lambdaCounter++;
            string elParam = $"el_{lambdaIdx}";
            string wsParam = $"ws_{lambdaIdx}";

            // Replace placeholders
            string call = perElementCall.Replace("{H}", H).Replace("{el}", elParam).Replace("{ws}", wsParam);

            string v = NextVar();
            if (returnsElement)
            {
                L(sb, indent, $"var {v} = {H}.{helperName}({inputVar}, static (JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
                L(sb, indent, "{");
                L(sb, indent + "    ", $"return {call};");
            }
            else
            {
                L(sb, indent, $"var {v} = {H}.{helperName}({inputVar}, static (JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
                L(sb, indent, "{");
                L(sb, indent + "    ", $"return {H}.IsTruthy({call});");
            }

            L(sb, indent, $"}}, {wsVar});");
            return v;
        }

        // ── HOF: $sort ───────────────────────────────────────
        private string EmitHofSort(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar, string wsVar)
        {
            if (func.Arguments.Count == 1)
            {
                // $sort(array) — default sort, no comparator
                string sortInput = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);
                string sortResult = NextVar();
                L(sb, indent, $"var {sortResult} = {H}.SortDefault({sortInput}, {wsVar});");
                return sortResult;
            }

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

        private string EmitBuiltinSingle(
            StringBuilder sb, FunctionCallNode func, string indent, string dataVar, string wsVar)
        {
            if (func.Arguments.Count == 1)
            {
                return EmitBuiltinUnary(sb, func, indent, dataVar, wsVar, "Single");
            }

            if (func.Arguments.Count != 2)
            {
                throw new JsonataException("T0410", "Arguments of function 'single' do not match function signature", 0);
            }

            // 2-arg form: $single(array, predicate)
            // Handle lambda or built-in ref
            string inputVar = EmitExpression(sb, func.Arguments[0], indent, dataVar, wsVar);

            if (func.Arguments[1] is LambdaNode lambda)
            {
                if (lambda.Parameters.Count < 1)
                {
                    throw new FallbackException();
                }

                int lambdaIdx = _lambdaCounter++;
                string elParam = $"el_{lambdaIdx}";
                string wsParam = $"ws_{lambdaIdx}";
                string innerIndent = indent + "    ";
                StringBuilder lambdaBody = new();

                string? savedVar = StashVariable(lambda.Parameters[0], elParam);

                bool hasIndex = lambda.Parameters.Count >= 2;
                bool hasArray = lambda.Parameters.Count >= 3;
                string? idxParam = hasIndex ? $"idx_{lambdaIdx}" : null;
                string? arrParam = hasArray ? $"arr_{lambdaIdx}" : null;
                string? savedIdx = hasIndex ? StashVariable(lambda.Parameters[1], idxParam!) : null;
                string? savedArr = hasArray ? StashVariable(lambda.Parameters[2], arrParam!) : null;

                bool prevRootRef = _usesRootRef;
                _usesRootRef = true;
                string bodyResult = EmitExpression(lambdaBody, lambda.Body, innerIndent, elParam, wsParam);
                _usesRootRef = prevRootRef;

                if (hasArray)
                {
                    RestoreVariable(lambda.Parameters[2], savedArr);
                }

                if (hasIndex)
                {
                    RestoreVariable(lambda.Parameters[1], savedIdx);
                }

                RestoreVariable(lambda.Parameters[0], savedVar);

                string v = NextVar();
                if (hasIndex)
                {
                    L(sb, indent, $"var {v} = {H}.SingleWithPredicateIndexed({inputVar}, (JsonElement {elParam}, JsonElement {idxParam}, JsonElement {arrParam ?? $"_arr_{lambdaIdx}"}, JsonWorkspace {wsParam}) =>");
                }
                else
                {
                    L(sb, indent, $"var {v} = {H}.SingleWithPredicate({inputVar}, (JsonElement {elParam}, JsonWorkspace {wsParam}) =>");
                }

                L(sb, indent, "{");
                sb.Append(lambdaBody);
                L(sb, innerIndent, $"return {H}.IsTruthy({bodyResult});");
                L(sb, indent, $"}}, {wsVar});");
                return v;
            }
            else if (func.Arguments[1] is VariableNode builtinRef && IsBuiltinFunctionName(builtinRef.Name))
            {
                // $single(arr, $boolean) etc.
                string? call = builtinRef.Name switch
                {
                    "boolean" => $"{H}.IsTruthy({{el}})",
                    "not" => $"!{H}.IsTruthy({{el}})",
                    _ => null,
                };

                if (call is null)
                {
                    throw new FallbackException();
                }

                int lambdaIdx = _lambdaCounter++;
                string elParam = $"el_{lambdaIdx}";
                string wsParam = $"ws_{lambdaIdx}";

                string callExpanded = call.Replace("{el}", elParam);
                string v = NextVar();
                L(sb, indent, $"var {v} = {H}.SingleWithPredicate({inputVar}, static (JsonElement {elParam}, JsonWorkspace {wsParam}) => {callExpanded}, {wsVar});");
                return v;
            }

            throw new FallbackException();
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