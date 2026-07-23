// <copyright file="WorkflowDocumentAnalyzer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Collects positioned semantic diagnostics over an Arazzo workflow document — the designer's
/// validate pass (workflow-designer design §4.1). Where the code generator fails fast on the first
/// broken construct (an unknown <c>goto</c> target throws mid-emit), this analyzer walks the whole
/// document and reports <em>every</em> finding with a JSON Pointer, a severity, and a category, so
/// the Problems tray and the text editor's markers can show them all at once.
/// </summary>
/// <remarks>
/// <para>The checks mirror the generator's rules (<see cref="ControlFlowEmitter"/> /
/// <see cref="WorkflowExecutorEmitter"/>) plus the holes the generator is silent about (an unknown
/// <c>dependsOn</c> reference is ignored at emit; here it is an error). Criterion syntax is checked
/// with the runtime's own compiler (<see cref="CompiledCriterion.Compile"/>), so a criterion that
/// passes here evaluates at run time. Source-dependent checks (unresolved
/// <c>operationId</c>/<c>operationPath</c>/<c>channelPath</c>) need the working copy's attached
/// sources and are not performed here.</para>
/// <para>The analyzer is tolerant by design: it walks whatever shape it finds and never throws on a
/// malformed document — structural conformance is the JSON-Schema pass's job (the control plane
/// runs both and merges the diagnostics).</para>
/// <para>Pointer segments accumulate in a single grow-once buffer with push/pop scope discipline;
/// a pointer <em>string</em> materializes only when a finding is actually emitted (with its
/// human-facing message — the per-finding leaves), so a clean document allocates no paths at all.</para>
/// </remarks>
public static class WorkflowDocumentAnalyzer
{
    /// <summary>Analyzes an Arazzo document, collecting every semantic diagnostic.</summary>
    /// <param name="document">The Arazzo document's root JSON value.</param>
    /// <returns>The diagnostics, in document order (empty when clean).</returns>
    public static IReadOnlyList<WorkflowDocumentDiagnostic> Analyze(in JsonElement document)
    {
        var diagnostics = new List<WorkflowDocumentDiagnostic>();
        if (document.ValueKind != JsonValueKind.Object)
        {
            return diagnostics;
        }

        JsonElement components = document.TryGetProperty("components"u8, out JsonElement c) ? c : default;

        // The document's workflow ids (for goto workflowId / workflow-level dependsOn), plus whether
        // arazzo-type sources exist (an unknown workflowId may live in one — soften to a warning).
        var workflowIds = new HashSet<string>(StringComparer.Ordinal);
        bool hasArazzoSources = false;
        if (document.TryGetProperty("sourceDescriptions"u8, out JsonElement sources) && sources.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement source in sources.EnumerateArray())
            {
                if (source.ValueKind == JsonValueKind.Object
                    && source.TryGetProperty("type"u8, out JsonElement st)
                    && st.ValueKind == JsonValueKind.String
                    && st.ValueEquals("arazzo"u8))
                {
                    hasArazzoSources = true;
                }
            }
        }

        if (!document.TryGetProperty("workflows"u8, out JsonElement workflows) || workflows.ValueKind != JsonValueKind.Array)
        {
            return diagnostics;
        }

        var pointers = new PointerBuilder();
        using PointerScope workflowsScope = pointers.Push("workflows");

        int wi = 0;
        foreach (JsonElement workflow in workflows.EnumerateArray())
        {
            if (workflow.ValueKind == JsonValueKind.Object
                && workflow.TryGetProperty("workflowId"u8, out JsonElement id)
                && id.ValueKind == JsonValueKind.String
                && id.GetString() is { Length: > 0 } workflowId
                && !workflowIds.Add(workflowId))
            {
                using PointerScope indexScope = pointers.Push(wi);
                diagnostics.Add(new(
                    WorkflowDocumentDiagnosticSeverity.Error,
                    "duplicate-id",
                    pointers.Materialize("workflowId"),
                    $"Duplicate workflowId '{workflowId}' — workflow ids must be unique within the document."));
            }

            wi++;
        }

        wi = 0;
        foreach (JsonElement workflow in workflows.EnumerateArray())
        {
            using PointerScope indexScope = pointers.Push(wi);
            AnalyzeWorkflow(workflow, pointers, workflowIds, hasArazzoSources, components, diagnostics);
            wi++;
        }

        return diagnostics;
    }

    private static void AnalyzeWorkflow(
        in JsonElement workflow,
        PointerBuilder pointers,
        HashSet<string> workflowIds,
        bool hasArazzoSources,
        in JsonElement components,
        List<WorkflowDocumentDiagnostic> diagnostics)
    {
        if (workflow.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // Workflow-level dependsOn names workflows.
        CheckDependsOn(workflow, pointers, workflowIds, hasArazzoSources, isWorkflow: true, diagnostics);

        // The workflow's step ids (goto stepId targets, step dependsOn, reachability), and — for
        // resolving $steps.<id>.outputs.<name> references — the set of output NAMES each step declares.
        var stepIds = new List<string>();
        var declaredOutputsByStep = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        JsonElement steps = workflow.TryGetProperty("steps"u8, out JsonElement s) && s.ValueKind == JsonValueKind.Array ? s : default;
        if (steps.ValueKind == JsonValueKind.Array)
        {
            using PointerScope stepsScope = pointers.Push("steps");
            int si = 0;
            foreach (JsonElement step in steps.EnumerateArray())
            {
                string? stepId = ReadString(step, "stepId"u8);
                if (stepId is { Length: > 0 })
                {
                    if (stepIds.Contains(stepId))
                    {
                        using PointerScope indexScope = pointers.Push(si);
                        diagnostics.Add(new(
                            WorkflowDocumentDiagnosticSeverity.Error,
                            "duplicate-id",
                            pointers.Materialize("stepId"),
                            $"Duplicate stepId '{stepId}' — step ids must be unique within the workflow."));
                    }
                    else
                    {
                        stepIds.Add(stepId);
                        declaredOutputsByStep[stepId] = DeclaredOutputNames(step);
                    }
                }

                si++;
            }
        }

        var stepIdSet = new HashSet<string>(stepIds, StringComparer.Ordinal);
        var flows = new Dictionary<int, StepFlow>(); // step index → reachability edges

        // Workflow-level success/failure action defaults.
        CheckActions(workflow, "successActions"u8, "successActions", pointers, stepIdSet, workflowIds, hasArazzoSources, components, null, successList: true, declaredOutputsByStep, diagnostics);
        CheckActions(workflow, "failureActions"u8, "failureActions", pointers, stepIdSet, workflowIds, hasArazzoSources, components, null, successList: false, declaredOutputsByStep, diagnostics);

        // Workflow outputs are runtime expressions.
        CheckOutputs(workflow, pointers, declaredOutputsByStep, diagnostics);

        if (steps.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        using (PointerScope stepsScope = pointers.Push("steps"))
        {
            int index = 0;
            foreach (JsonElement step in steps.EnumerateArray())
            {
                if (step.ValueKind == JsonValueKind.Object)
                {
                    using PointerScope indexScope = pointers.Push(index);
                    var flow = new StepFlow();
                    flows[index] = flow;

                    CheckDependsOn(step, pointers, stepIdSet, hasArazzoSources: false, isWorkflow: false, diagnostics);
                    CheckCriteria(step, "successCriteria"u8, "successCriteria", pointers, declaredOutputsByStep, diagnostics);
                    CheckActions(step, "onSuccess"u8, "onSuccess", pointers, stepIdSet, workflowIds, hasArazzoSources, components, flow, successList: true, declaredOutputsByStep, diagnostics);
                    CheckActions(step, "onFailure"u8, "onFailure", pointers, stepIdSet, workflowIds, hasArazzoSources, components, flow, successList: false, declaredOutputsByStep, diagnostics);
                    CheckParameters(step, pointers, components, declaredOutputsByStep, diagnostics);
                    CheckOutputs(step, pointers, declaredOutputsByStep, diagnostics);
                    CheckRequestBody(step, pointers, declaredOutputsByStep, diagnostics);
                }

                index++;
            }
        }

        CheckImplicitDependencies(steps, pointers, stepIds, diagnostics);
        CheckReachability(steps, pointers, stepIds, flows, diagnostics);
    }

    // ── implicit dependencies ($steps.<id>.* runtime references; Arazzo 1.1 §5.8.5.2.4) ──────────────
    // A step that reads $steps.<id>.* implicitly depends on <id>. The generator folds those edges into
    // the topological order (WorkflowExecutorEmitter.TopologicallyOrder), so this pass mirrors what it
    // would do: an unknown $steps target is an error (silently undefined at run time otherwise); the
    // combined dependsOn+implicit graph being cyclic is an error; and — in a workflow that declares no
    // dependsOn at all — an implicit dependency that forces a reorder is a warning, because the step
    // array is almost certainly mis-ordered even though the run will still succeed after the sort.
    private static void CheckImplicitDependencies(
        in JsonElement steps,
        PointerBuilder pointers,
        List<string> stepIds,
        List<WorkflowDocumentDiagnostic> diagnostics)
    {
        if (steps.ValueKind != JsonValueKind.Array || stepIds.Count == 0)
        {
            return;
        }

        var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        int si = 0;
        foreach (JsonElement step in steps.EnumerateArray())
        {
            if (ReadString(step, "stepId"u8) is { Length: > 0 } id)
            {
                indexById.TryAdd(id, si);
            }

            si++;
        }

        int count = si;
        var inDegree = new int[count];
        var dependents = new List<int>[count];
        for (int i = 0; i < count; i++)
        {
            dependents[i] = [];
        }

        var edgeSet = new HashSet<(int From, int To)>();
        void AddEdge(int from, int to)
        {
            if (from != to && edgeSet.Add((from, to)))
            {
                dependents[from].Add(to);
                inDegree[to]++;
            }
        }

        bool anyDependsOn = false;
        int reorderStepIndex = -1;
        var references = new HashSet<string>(StringComparer.Ordinal);

        using (PointerScope stepsScope = pointers.Push("steps"))
        {
            si = 0;
            foreach (JsonElement step in steps.EnumerateArray())
            {
                int self = si;
                si++;
                if (step.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                // Explicit dependsOn contributes to the same graph (unknown targets already reported by CheckDependsOn).
                if (step.TryGetProperty("dependsOn"u8, out JsonElement deps) && deps.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement dep in deps.EnumerateArray())
                    {
                        if (dep.ValueKind == JsonValueKind.String && dep.GetString() is { Length: > 0 } dn && indexById.TryGetValue(dn, out int di))
                        {
                            anyDependsOn = true;
                            AddEdge(di, self);
                        }
                    }
                }

                references.Clear();
                CollectStepReferences(step, references);
                foreach (string reference in references)
                {
                    if (indexById.TryGetValue(reference, out int ri))
                    {
                        if (ri > self && reorderStepIndex < 0)
                        {
                            reorderStepIndex = self;
                        }

                        AddEdge(ri, self);
                    }
                    else
                    {
                        using PointerScope indexScope = pointers.Push(self);
                        diagnostics.Add(new(
                            WorkflowDocumentDiagnosticSeverity.Error,
                            "implicit-dependency",
                            pointers.Materialize(),
                            $"A runtime expression references $steps.{reference}, which is not a step in this workflow (it resolves to nothing at run time)."));
                    }
                }
            }
        }

        // Kahn's algorithm over the combined graph; a shortfall means a cycle.
        var ready = new SortedSet<int>();
        for (int i = 0; i < count; i++)
        {
            if (inDegree[i] == 0)
            {
                ready.Add(i);
            }
        }

        int emitted = 0;
        while (ready.Count > 0)
        {
            int next = ready.Min;
            ready.Remove(next);
            emitted++;
            foreach (int dependent in dependents[next])
            {
                if (--inDegree[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        if (emitted != count)
        {
            using PointerScope stepsScope = pointers.Push("steps");
            diagnostics.Add(new(
                WorkflowDocumentDiagnosticSeverity.Error,
                "dependency-cycle",
                pointers.Materialize(),
                "The steps form a dependency cycle (dependsOn together with implicit $steps references); they cannot be ordered."));
        }
        else if (reorderStepIndex >= 0 && !anyDependsOn)
        {
            using PointerScope stepsScope = pointers.Push("steps");
            using PointerScope indexScope = pointers.Push(reorderStepIndex);
            diagnostics.Add(new(
                WorkflowDocumentDiagnosticSeverity.Warning,
                "implicit-dependency",
                pointers.Materialize(),
                "This step reads a later-declared step's outputs via a runtime expression, so it will be reordered to run after that step. Declare the steps in dependency order (or add dependsOn) to make the order explicit."));
        }
    }

    // ── dependsOn (workflow-level names workflows; step-level names steps) ────────────────────────
    private static void CheckDependsOn(
        in JsonElement owner,
        PointerBuilder pointers,
        HashSet<string> knownIds,
        bool hasArazzoSources,
        bool isWorkflow,
        List<WorkflowDocumentDiagnostic> diagnostics)
    {
        if (!owner.TryGetProperty("dependsOn"u8, out JsonElement dependsOn) || dependsOn.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int i = 0;
        foreach (JsonElement dependency in dependsOn.EnumerateArray())
        {
            if (dependency.ValueKind == JsonValueKind.String && dependency.GetString() is { Length: > 0 } name && !knownIds.Contains(name))
            {
                string kind = isWorkflow ? "workflow" : "step";

                // A workflow dependency may live in an arazzo-type source this analyzer cannot see.
                if (isWorkflow && hasArazzoSources)
                {
                    diagnostics.Add(new(
                        WorkflowDocumentDiagnosticSeverity.Warning,
                        "depends-on",
                        pointers.Materialize("dependsOn", i),
                        $"dependsOn references '{name}', which is not a {kind} in this document (it may be defined by an arazzo-type source)."));
                }
                else
                {
                    // The generator silently ignores an unknown dependsOn — surfacing it here is the fix.
                    diagnostics.Add(new(
                        WorkflowDocumentDiagnosticSeverity.Error,
                        "depends-on",
                        pointers.Materialize("dependsOn", i),
                        $"dependsOn references unknown {kind} '{name}'."));
                }
            }

            i++;
        }
    }

    // ── success/failure actions (inline or reusable reference) ────────────────────────────────────
    private static void CheckActions(
        in JsonElement owner,
        ReadOnlySpan<byte> property,
        string segment,
        PointerBuilder pointers,
        HashSet<string> stepIds,
        HashSet<string> workflowIds,
        bool hasArazzoSources,
        in JsonElement components,
        StepFlow? flow,
        bool successList,
        IReadOnlyDictionary<string, HashSet<string>> declaredOutputsByStep,
        List<WorkflowDocumentDiagnostic> diagnostics)
    {
        if (!owner.TryGetProperty(property, out JsonElement actions) || actions.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        using PointerScope listScope = pointers.Push(segment);
        int i = 0;
        foreach (JsonElement entry in actions.EnumerateArray())
        {
            using PointerScope indexScope = pointers.Push(i);
            i++;
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            JsonElement action = entry;
            if (ReadString(entry, "reference"u8) is { } reference)
            {
                // A reusable-action reference: resolve it against components; the resolved action's
                // own content is checked at its component location by the author's tooling, but its
                // goto target participates in THIS workflow, so check it here when it resolves.
                if (ResolveReusableReference(components, reference) is not { } resolved)
                {
                    diagnostics.Add(new(
                        WorkflowDocumentDiagnosticSeverity.Error,
                        "component-reference",
                        pointers.Materialize("reference"),
                        $"Could not resolve reusable reference '{reference}' against the document's components."));
                    continue;
                }

                action = resolved;
            }

            string? type = ReadString(action, "type"u8);
            string? targetStep = ReadString(action, "stepId"u8);
            string? targetWorkflow = ReadString(action, "workflowId"u8);
            bool hasCriteria = action.TryGetProperty("criteria"u8, out JsonElement actionCriteria)
                && actionCriteria.ValueKind == JsonValueKind.Array
                && actionCriteria.GetArrayLength() > 0;

            // Reachability edges: dispatch is first-match-wins, and the default "continue to the
            // next step" fires only when NO success action matches — so a criteria-less (catch-all)
            // success action that ends or jumps blocks the fall-through edge entirely.
            if (flow is { } f && successList && !hasCriteria && type is "goto" or "end")
            {
                f.BlocksFallThrough = true;
            }

            if (type is "goto" or "retry")
            {
                if (targetStep is { Length: > 0 })
                {
                    if (!stepIds.Contains(targetStep))
                    {
                        diagnostics.Add(new(
                            WorkflowDocumentDiagnosticSeverity.Error,
                            "goto-target",
                            pointers.Materialize(),
                            $"The {type} action targets unknown step '{targetStep}'."));
                    }
                    else
                    {
                        flow?.Targets.Add(targetStep);
                    }
                }

                if (targetWorkflow is { Length: > 0 } && !workflowIds.Contains(targetWorkflow))
                {
                    diagnostics.Add(new(
                        hasArazzoSources ? WorkflowDocumentDiagnosticSeverity.Warning : WorkflowDocumentDiagnosticSeverity.Error,
                        "goto-target",
                        pointers.Materialize(),
                        $"The {type} action targets workflow '{targetWorkflow}', which is not defined in this document"
                        + (hasArazzoSources ? " (it may be defined by an arazzo-type source)." : ".")));
                }
            }

            CheckCriteria(action, "criteria"u8, "criteria", pointers, declaredOutputsByStep, diagnostics);
        }
    }

    // ── criteria (validated with the runtime's own compiler) ──────────────────────────────────────
    private static void CheckCriteria(in JsonElement owner, ReadOnlySpan<byte> property, string segment, PointerBuilder pointers, IReadOnlyDictionary<string, HashSet<string>> declaredOutputsByStep, List<WorkflowDocumentDiagnostic> diagnostics)
    {
        if (!owner.TryGetProperty(property, out JsonElement criteria) || criteria.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        using PointerScope listScope = pointers.Push(segment);
        int i = 0;
        foreach (JsonElement criterion in criteria.EnumerateArray())
        {
            using PointerScope indexScope = pointers.Push(i);
            CheckCriterion(criterion, pointers, declaredOutputsByStep, diagnostics);
            i++;
        }
    }

    private static void CheckCriterion(in JsonElement criterion, PointerBuilder pointers, IReadOnlyDictionary<string, HashSet<string>> declaredOutputsByStep, List<WorkflowDocumentDiagnostic> diagnostics)
    {
        if (criterion.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // The type may be the shorthand string or the {type, version} object form.
        string type = "simple";
        if (criterion.TryGetProperty("type"u8, out JsonElement typeElement))
        {
            type = (typeElement.ValueKind == JsonValueKind.Object ? ReadString(typeElement, "type"u8) : typeElement.ValueKind == JsonValueKind.String ? typeElement.GetString() : null) ?? "simple";
        }

        string? condition = ReadString(criterion, "condition"u8);
        string? context = ReadString(criterion, "context"u8);

        if (context is { Length: > 0 })
        {
            CheckExpression(context, pointers, "context", declaredOutputsByStep, diagnostics);
        }

        if (type == "xpath")
        {
            // xpath round-trips through the designer but this runtime does not evaluate it (design §1).
            diagnostics.Add(new(
                WorkflowDocumentDiagnosticSeverity.Warning,
                "criterion-type",
                pointers.Materialize(),
                "This runtime does not evaluate xpath criteria; the criterion is preserved but a run will not honour it."));
            return;
        }

        if (condition is null)
        {
            return; // requiredness is the schema pass's finding, with the schema's own message
        }

        CriterionType criterionType = type switch
        {
            "regex" => CriterionType.Regex,
            "jsonpath" => CriterionType.JsonPath,
            _ => CriterionType.Simple,
        };

        try
        {
            // The runtime's own compiler: simple-condition syntax (with character positions), the
            // ECMA regex translation, and the context requirement all check exactly as a run would.
            CompiledCriterion.Compile(criterionType, condition, context);
        }
        catch (FormatException ex)
        {
            diagnostics.Add(new(WorkflowDocumentDiagnosticSeverity.Error, "criterion-syntax", pointers.Materialize("condition"), ex.Message));
        }
        catch (ArgumentException ex)
        {
            diagnostics.Add(new(WorkflowDocumentDiagnosticSeverity.Error, "criterion-syntax", pointers.Materialize(), ex.Message));
        }
    }

    // ── runtime expressions ───────────────────────────────────────────────────────────────────────
    private static void CheckExpression(string value, PointerBuilder pointers, string segment, IReadOnlyDictionary<string, HashSet<string>> declaredOutputsByStep, List<WorkflowDocumentDiagnostic> diagnostics)
    {
        // ArazzoExpression.Parse never throws: anything malformed comes back as Literal. A value that
        // BEGINS with '$' is an expression by intent, so a Literal parse means it is malformed.
        if (value.StartsWith('$') && ArazzoExpression.Parse(value).Source == ArazzoExpressionSource.Literal)
        {
            diagnostics.Add(new(
                WorkflowDocumentDiagnosticSeverity.Error,
                "expression-syntax",
                pointers.Materialize(segment),
                $"'{value}' is not a valid runtime expression."));
        }

        // Regardless of the bare-vs-interpolated form, a $steps.<id>.outputs.<name> navigation must name
        // an output the referenced step actually declares — an undeclared one resolves to nothing at run
        // time (a bound path parameter then renders as an empty segment, e.g. /accounts//identity).
        CheckOutputReferences(value, pointers, segment, declaredOutputsByStep, diagnostics);
    }

    // Reports every $steps.<id>.outputs.<name> reference in <paramref name="value"/> whose <id> is a
    // known step (an unknown one is already an implicit-dependency error) but does not declare <name>.
    private static void CheckOutputReferences(string value, PointerBuilder pointers, string segment, IReadOnlyDictionary<string, HashSet<string>> declaredOutputsByStep, List<WorkflowDocumentDiagnostic> diagnostics)
    {
        const string prefix = "$steps.";
        const string mid = ".outputs.";
        int i = 0;
        while ((i = value.IndexOf(prefix, i, StringComparison.Ordinal)) >= 0)
        {
            int idStart = i + prefix.Length;
            int idEnd = idStart;
            while (idEnd < value.Length && (char.IsAsciiLetterOrDigit(value[idEnd]) || value[idEnd] is '_' or '-'))
            {
                idEnd++;
            }

            i = idEnd;
            if (idEnd == idStart || idEnd + mid.Length > value.Length || !value.AsSpan(idEnd, mid.Length).SequenceEqual(mid))
            {
                continue; // not a "$steps.<id>.outputs.<...>" navigation
            }

            string stepId = value[idStart..idEnd];
            if (!declaredOutputsByStep.TryGetValue(stepId, out HashSet<string>? declared))
            {
                continue; // unknown step — CheckImplicitDependencies already reports it
            }

            // The output name runs through the Arazzo name grammar [A-Za-z0-9._-]; a '.' inside is
            // ambiguous (a dotted output name vs. navigation into the value), so DeclaresOutput clears
            // the reference when ANY declared name is a boundary-prefix of the remainder.
            int nameStart = idEnd + mid.Length;
            int nameEnd = nameStart;
            while (nameEnd < value.Length && (char.IsAsciiLetterOrDigit(value[nameEnd]) || value[nameEnd] is '.' or '_' or '-'))
            {
                nameEnd++;
            }

            if (nameEnd == nameStart)
            {
                continue; // "$steps.<id>.outputs." with no name — a whole-map reference, not ours to judge
            }

            string remainder = value[nameStart..nameEnd];
            if (DeclaresOutput(declared, remainder))
            {
                continue;
            }

            int dot = remainder.IndexOf('.');
            string apparent = dot < 0 ? remainder : remainder[..dot];
            diagnostics.Add(new(
                WorkflowDocumentDiagnosticSeverity.Error,
                "output-reference",
                pointers.Materialize(segment),
                $"'$steps.{stepId}.outputs.{apparent}' references an output that step '{stepId}' does not declare (it resolves to nothing at run time)."));
        }
    }

    // True when a declared output name is a boundary-prefix of the reference remainder: the whole name
    // (an exact hit), or the name followed by '.' (navigation into that output's value).
    private static bool DeclaresOutput(HashSet<string> declared, string remainder)
    {
        foreach (string name in declared)
        {
            if (remainder.Length < name.Length || !remainder.AsSpan(0, name.Length).SequenceEqual(name))
            {
                continue;
            }

            if (remainder.Length == name.Length || remainder[name.Length] == '.')
            {
                return true;
            }
        }

        return false;
    }

    // The set of output NAMES an outputs-bearing object (a step or workflow) declares.
    private static HashSet<string> DeclaredOutputNames(in JsonElement owner)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (owner.TryGetProperty("outputs"u8, out JsonElement outputs) && outputs.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> output in outputs.EnumerateObject())
            {
                names.Add(output.Name);
            }
        }

        return names;
    }

    private static void CheckOutputs(in JsonElement owner, PointerBuilder pointers, IReadOnlyDictionary<string, HashSet<string>> declaredOutputsByStep, List<WorkflowDocumentDiagnostic> diagnostics)
    {
        if (!owner.TryGetProperty("outputs"u8, out JsonElement outputs) || outputs.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        using PointerScope outputsScope = pointers.Push("outputs");
        foreach (JsonProperty<JsonElement> output in outputs.EnumerateObject())
        {
            if (output.Value.ValueKind == JsonValueKind.String && output.Value.GetString() is { Length: > 0 } value)
            {
                CheckExpression(value, pointers, output.Name, declaredOutputsByStep, diagnostics);
            }
        }
    }

    private static void CheckParameters(in JsonElement step, PointerBuilder pointers, in JsonElement components, IReadOnlyDictionary<string, HashSet<string>> declaredOutputsByStep, List<WorkflowDocumentDiagnostic> diagnostics)
    {
        if (!step.TryGetProperty("parameters"u8, out JsonElement parameters) || parameters.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        using PointerScope listScope = pointers.Push("parameters");
        int i = 0;
        foreach (JsonElement parameter in parameters.EnumerateArray())
        {
            using PointerScope indexScope = pointers.Push(i);
            i++;
            if (parameter.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (ReadString(parameter, "reference"u8) is { } reference)
            {
                if (ResolveReusableReference(components, reference) is null)
                {
                    diagnostics.Add(new(
                        WorkflowDocumentDiagnosticSeverity.Error,
                        "component-reference",
                        pointers.Materialize("reference"),
                        $"Could not resolve reusable reference '{reference}' against the document's components."));
                }

                continue;
            }

            if (ReadString(parameter, "value"u8) is { Length: > 0 } value)
            {
                CheckExpression(value, pointers, "value", declaredOutputsByStep, diagnostics);
            }
        }
    }

    private static void CheckRequestBody(in JsonElement step, PointerBuilder pointers, IReadOnlyDictionary<string, HashSet<string>> declaredOutputsByStep, List<WorkflowDocumentDiagnostic> diagnostics)
    {
        if (!step.TryGetProperty("requestBody"u8, out JsonElement requestBody) || requestBody.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        using PointerScope bodyScope = pointers.Push("requestBody");
        if (requestBody.TryGetProperty("payload"u8, out JsonElement payload)
            && payload.ValueKind == JsonValueKind.String
            && payload.GetString() is { Length: > 0 } payloadValue)
        {
            CheckExpression(payloadValue, pointers, "payload", declaredOutputsByStep, diagnostics);
        }

        if (requestBody.TryGetProperty("replacements"u8, out JsonElement replacements) && replacements.ValueKind == JsonValueKind.Array)
        {
            using PointerScope listScope = pointers.Push("replacements");
            int i = 0;
            foreach (JsonElement replacement in replacements.EnumerateArray())
            {
                if (replacement.ValueKind == JsonValueKind.Object
                    && ReadString(replacement, "value"u8) is { Length: > 0 } value)
                {
                    using PointerScope indexScope = pointers.Push(i);
                    CheckExpression(value, pointers, "value", declaredOutputsByStep, diagnostics);
                }

                i++;
            }
        }
    }

    // Collects the step ids that <paramref name="step"/> references through a $steps.<id>.* runtime
    // expression on any of its expression-bearing surfaces (parameters, requestBody, success criteria,
    // inline onSuccess/onFailure criteria, outputs) — the same surfaces the generator reads.
    private static void CollectStepReferences(in JsonElement step, HashSet<string> into)
    {
        if (step.TryGetProperty("parameters"u8, out JsonElement parameters) && parameters.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement parameter in parameters.EnumerateArray())
            {
                AddExpressionReferences(ReadString(parameter, "value"u8), into);
            }
        }

        if (step.TryGetProperty("requestBody"u8, out JsonElement body) && body.ValueKind == JsonValueKind.Object)
        {
            AddExpressionReferences(ReadString(body, "payload"u8), into);
            if (body.TryGetProperty("replacements"u8, out JsonElement replacements) && replacements.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement replacement in replacements.EnumerateArray())
                {
                    AddExpressionReferences(ReadString(replacement, "value"u8), into);
                }
            }
        }

        CollectCriteriaReferences(step, "successCriteria"u8, into);
        CollectActionCriteriaReferences(step, "onSuccess"u8, into);
        CollectActionCriteriaReferences(step, "onFailure"u8, into);

        if (step.TryGetProperty("outputs"u8, out JsonElement outputs) && outputs.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> output in outputs.EnumerateObject())
            {
                if (output.Value.ValueKind == JsonValueKind.String)
                {
                    AddExpressionReferences(output.Value.GetString(), into);
                }
            }
        }
    }

    private static void CollectCriteriaReferences(in JsonElement owner, ReadOnlySpan<byte> property, HashSet<string> into)
    {
        if (owner.TryGetProperty(property, out JsonElement criteria) && criteria.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement criterion in criteria.EnumerateArray())
            {
                if (criterion.ValueKind == JsonValueKind.Object)
                {
                    AddExpressionReferences(ReadString(criterion, "condition"u8), into);
                    AddExpressionReferences(ReadString(criterion, "context"u8), into);
                }
            }
        }
    }

    private static void CollectActionCriteriaReferences(in JsonElement owner, ReadOnlySpan<byte> property, HashSet<string> into)
    {
        if (owner.TryGetProperty(property, out JsonElement actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement action in actions.EnumerateArray())
            {
                if (action.ValueKind == JsonValueKind.Object)
                {
                    CollectCriteriaReferences(action, "criteria"u8, into);
                }
            }
        }
    }

    // Adds every $steps.<id>. step id found in <paramref name="expression"/> (bare or interpolated,
    // possibly several) to <paramref name="into"/>. A step id is [A-Za-z0-9_-]+ delimited by the '.'.
    private static void AddExpressionReferences(string? expression, HashSet<string> into)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return;
        }

        const string prefix = "$steps.";
        int i = 0;
        while ((i = expression!.IndexOf(prefix, i, StringComparison.Ordinal)) >= 0)
        {
            int idStart = i + prefix.Length;
            int end = idStart;
            while (end < expression.Length && (char.IsAsciiLetterOrDigit(expression[end]) || expression[end] == '_' || expression[end] == '-'))
            {
                end++;
            }

            if (end > idStart && end < expression.Length && expression[end] == '.')
            {
                into.Add(expression[idStart..end]);
            }

            i = idStart;
        }
    }

    // ── reachability ──────────────────────────────────────────────────────────────────────────────
    // Conservative BFS from the first step. A step's successors are its goto targets (success AND
    // failure — conditional or not) plus the sequential fall-through, EXCEPT when a criteria-less
    // (catch-all) success action ends or jumps: dispatch is first-match-wins and the default
    // "continue" can then never fire. Failure paths never fall through in Arazzo (an unhandled
    // failure faults the run), and workflow-level defaults are ignored here (over-approximating
    // reachability keeps this a warning with no false flags).
    private static void CheckReachability(
        in JsonElement steps,
        PointerBuilder pointers,
        List<string> stepIds,
        Dictionary<int, StepFlow> flows,
        List<WorkflowDocumentDiagnostic> diagnostics)
    {
        if (stepIds.Count == 0 || steps.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        // index by stepId for goto edges (duplicate ids already reported; first occurrence wins).
        var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        int si = 0;
        foreach (JsonElement step in steps.EnumerateArray())
        {
            if (ReadString(step, "stepId"u8) is { Length: > 0 } stepId)
            {
                indexById.TryAdd(stepId, si);
            }

            si++;
        }

        int count = si;
        var reachable = new HashSet<int> { 0 };
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.TryDequeue(out int current))
        {
            StepFlow? flow = flows.GetValueOrDefault(current);
            if (flow is not { BlocksFallThrough: true } && current + 1 < count && reachable.Add(current + 1))
            {
                queue.Enqueue(current + 1);
            }

            if (flow is not null)
            {
                foreach (string target in flow.Targets)
                {
                    if (indexById.TryGetValue(target, out int t) && reachable.Add(t))
                    {
                        queue.Enqueue(t);
                    }
                }
            }
        }

        using PointerScope stepsScope = pointers.Push("steps");
        for (int i = 0; i < count; i++)
        {
            if (!reachable.Contains(i))
            {
                using PointerScope indexScope = pointers.Push(i);
                diagnostics.Add(new(
                    WorkflowDocumentDiagnosticSeverity.Warning,
                    "reachability",
                    pointers.Materialize(),
                    "No path reaches this step: no earlier step falls through or jumps to it (an unconditional success action diverts before it)."));
            }
        }
    }

    /// <summary>Resolves a reusable-object reference (<c>$components.&lt;kind&gt;.&lt;name&gt;</c>) against the components object.</summary>
    private static JsonElement? ResolveReusableReference(in JsonElement components, string reference)
    {
        if (components.ValueKind != JsonValueKind.Object || !reference.StartsWith("$components.", StringComparison.Ordinal))
        {
            return null;
        }

        JsonElement node = components;
        ReadOnlySpan<char> remaining = reference.AsSpan("$components.".Length);
        while (true)
        {
            int dot = remaining.IndexOf('.');
            ReadOnlySpan<char> segment = dot < 0 ? remaining : remaining[..dot];
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(segment, out node))
            {
                return null;
            }

            if (dot < 0)
            {
                return node;
            }

            remaining = remaining[(dot + 1)..];
        }
    }

    private static string? ReadString(in JsonElement owner, ReadOnlySpan<byte> property)
        => owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // The per-step reachability edges: resolved goto targets plus whether a catch-all success
    // action blocks the sequential fall-through.
    private sealed class StepFlow
    {
        public List<string> Targets { get; } = [];

        public bool BlocksFallThrough { get; set; }
    }

    // Pops a pushed pointer segment when its scope closes.
    private readonly ref struct PointerScope(PointerBuilder owner, int mark)
    {
        public void Dispose() => owner.PopTo(mark);
    }

    // Accumulates the current JSON Pointer in one grow-once char buffer; a pointer STRING
    // materializes only when a finding is emitted. Segments escape per RFC 6901 ('~'→'~0', '/'→'~1').
    private sealed class PointerBuilder
    {
        private char[] buffer = new char[128];
        private int length;

        public PointerScope Push(string segment)
        {
            int mark = this.length;
            this.AppendSegment(segment);
            return new PointerScope(this, mark);
        }

        public PointerScope Push(int index)
        {
            int mark = this.length;
            this.AppendIndex(index);
            return new PointerScope(this, mark);
        }

        public void PopTo(int mark) => this.length = mark;

        /// <summary>The current pointer as a string (a finding is being emitted).</summary>
        public string Materialize() => new(this.buffer, 0, this.length);

        /// <summary>The current pointer plus one trailing segment.</summary>
        public string Materialize(string segment)
        {
            int mark = this.length;
            this.AppendSegment(segment);
            string result = this.Materialize();
            this.length = mark;
            return result;
        }

        /// <summary>The current pointer plus a segment and an index (e.g. <c>…/dependsOn/2</c>).</summary>
        public string Materialize(string segment, int index)
        {
            int mark = this.length;
            this.AppendSegment(segment);
            this.AppendIndex(index);
            string result = this.Materialize();
            this.length = mark;
            return result;
        }

        private void AppendSegment(string segment)
        {
            this.EnsureCapacity(1 + (segment.Length * 2));
            this.buffer[this.length++] = '/';
            foreach (char c in segment)
            {
                if (c == '~')
                {
                    this.buffer[this.length++] = '~';
                    this.buffer[this.length++] = '0';
                }
                else if (c == '/')
                {
                    this.buffer[this.length++] = '~';
                    this.buffer[this.length++] = '1';
                }
                else
                {
                    this.buffer[this.length++] = c;
                }
            }
        }

        private void AppendIndex(int index)
        {
            this.EnsureCapacity(12);
            this.buffer[this.length++] = '/';
            index.TryFormat(this.buffer.AsSpan(this.length), out int written);
            this.length += written;
        }

        private void EnsureCapacity(int extra)
        {
            if (this.length + extra > this.buffer.Length)
            {
                Array.Resize(ref this.buffer, Math.Max(this.buffer.Length * 2, this.length + extra));
            }
        }
    }
}