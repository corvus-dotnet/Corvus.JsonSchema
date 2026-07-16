// <copyright file="WorkflowSimulator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// Deterministic workflow simulation (workflow-designer design §8, realising the
/// <c>ArazzoWorkflowEnginePlan.md</c> §3.2 facade): compiles a workflow document in memory through
/// the same <see cref="IWorkflowExecutorProvider"/> path the catalog uses, executes it against the
/// scripted <see cref="MockApiTransport"/> and a <see cref="ManualTimeProvider"/> virtual clock, and
/// returns the structured trace — step records with their request/response exchanges, post-hoc
/// criterion truth tables, inferred routing decisions, waits, and clock advances.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Stateless stepping (§8.2).</strong> There is no session state anywhere: every command
/// replays from the start to a stop condition. Determinism (scripted mocks + virtual clock + fixed
/// inputs) makes replays exact, so "step" is a replay with the stop one arrival further.
/// </para>
/// <para>
/// <strong>Compilation caching (§8.1).</strong> Compiles are keyed by a content hash over the
/// workflow and its sources and cached (bounded, least-recently-used) — one Roslyn compile per
/// document state, however many debug commands replay it.
/// </para>
/// <para>
/// <strong>Safety (§8.3).</strong> A step budget (enforced at the run's own checkpoint boundary), a
/// wall-clock cap, and the mock transport as the only I/O surface. No credentials exist here.
/// </para>
/// <para>
/// <strong>Simulated workflow.</strong> The executor provider compiles a document's FIRST workflow;
/// callers choose which workflow to simulate by reordering the document's <c>workflows</c> array
/// (the control-plane handler does exactly that). Message triggers are delivered through the run's
/// own delivered-message seam — no message transport is involved.
/// </para>
/// </remarks>
public sealed class WorkflowSimulator : IDisposable
{
    private readonly IWorkflowExecutorProvider provider;
    private readonly WorkflowExecutorLoader loader = new();
    private readonly Dictionary<string, LoadedWorkflow> cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> recency = new();
    private readonly int maxCachedExecutors;
    private readonly Lock cacheLock = new();

    /// <summary>Initializes a new instance of the <see cref="WorkflowSimulator"/> class.</summary>
    /// <param name="provider">The executor provider (the catalog-add compile path; MUST be a durable-mode provider — the checkpoint seam is the trace).</param>
    /// <param name="maxCachedExecutors">How many compiled document states stay loaded (least-recently-used beyond this).</param>
    public WorkflowSimulator(IWorkflowExecutorProvider provider, int maxCachedExecutors = 16)
    {
        ArgumentNullException.ThrowIfNull(provider);
        this.provider = provider;
        this.maxCachedExecutors = maxCachedExecutors;
    }

    /// <summary>Runs one simulation command: replay from the start to the stop condition.</summary>
    /// <param name="workflowUtf8">The Arazzo document (the workflow to simulate FIRST in its <c>workflows</c> array).</param>
    /// <param name="sources">The source documents by <c>sourceDescriptions</c> name.</param>
    /// <param name="scenario">The deterministic setup.</param>
    /// <param name="stop">The stop condition, if any.</param>
    /// <param name="budget">The safety limits (defaults applied when null).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The trace result; the caller disposes it after writing the trace out.</returns>
    public async ValueTask<SimulationResult> SimulateAsync(
        ReadOnlyMemory<byte> workflowUtf8,
        IReadOnlyList<KeyValuePair<string, byte[]>> sources,
        SimulationScenario scenario,
        SimulationStop? stop = null,
        SimulationBudget? budget = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        stop ??= new SimulationStop();
        budget ??= new SimulationBudget();

        LoadedWorkflow? loaded = this.GetOrCompile(workflowUtf8, sources);
        if (loaded is null)
        {
            return new SimulationResult(null)
            {
                Outcome = SimulationOutcome.NotExecutable,
                Steps = [],
                Exchanges = [],
                ClockAdvances = [],
                StepsExecuted = 0,
            };
        }

        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(workflowUtf8);
        WorkflowShape shape = WorkflowShape.From(document.RootElement);

        var owned = new List<IDisposable>();
        var transport = new MockApiTransport();
        foreach (SimulationMockRoute mock in scenario.Mocks)
        {
            if (!Enum.TryParse(mock.Method, ignoreCase: true, out OperationMethod method))
            {
                continue;
            }

            string body = mock.Body.ValueKind is JsonValueKind.Undefined ? string.Empty : mock.Body.GetRawText();
            transport.EnqueueResponse(method, mock.PathTemplate, mock.StatusCode, body);
        }

        var transports = new Dictionary<string, IApiTransport>(StringComparer.Ordinal);
        foreach (string name in shape.SourceNames)
        {
            transports[name] = transport;
        }

        var clock = new ManualTimeProvider();
        var clockAdvances = new List<SimulationClockAdvance>();
        var subWorkflowStepIds = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, WorkflowShape> entry in shape.Workflows)
        {
            subWorkflowStepIds[entry.Key] = entry.Value.StepIds;
        }

        var run = new TracingWorkflowRun(shape.StepIds, stop, budget.MaxSteps, clock, transport, BuildOverrides(shape, scenario), subWorkflowStepIds);

        // UNRENTED deliberately: the rented workspace cache is thread-affine, and this workspace's
        // lifetime is the SimulationResult's — disposed by the caller after awaits, possibly on
        // another thread. Renting here trips the cache's affinity assertion under load.
        var workspace = JsonWorkspace.CreateUnrented();
        owned.Add(workspace);

        SimulationOutcome outcome;
        if (run.PausedBefore is not null)
        {
            // The stop condition names the first step — paused before anything ran.
            outcome = SimulationOutcome.Paused;
        }
        else
        {
            using var wallClock = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            wallClock.CancelAfter(budget.WallClock);
            outcome = await this.RunToOutcomeAsync(loaded, transports, workspace, scenario, run, clock, clockAdvances, wallClock, cancellationToken).ConfigureAwait(false);
        }

        var result = new SimulationResult(new CompositeDisposable(owned))
        {
            Outcome = outcome,
            PausedBefore = outcome is SimulationOutcome.Paused or SimulationOutcome.BudgetExhausted ? run.PausedBefore : null,
            Outputs = run.WorkflowOutputs,
            Fault = run.RecordedFault,
            Wait = outcome == SimulationOutcome.Suspended ? run.RecordedWait : null,
            Steps = run.Steps,
            Exchanges = transport.Exchanges,
            ClockAdvances = clockAdvances,
            StepsExecuted = run.StepsExecuted,
        };

        AnalyzeTrace(shape, scenario, result, owned);
        return result;
    }

    /// <inheritdoc/>
    public void Dispose() => this.loader.Dispose();

    private async ValueTask<SimulationOutcome> RunToOutcomeAsync(
        LoadedWorkflow loaded,
        IReadOnlyDictionary<string, IApiTransport> transports,
        JsonWorkspace workspace,
        SimulationScenario scenario,
        TracingWorkflowRun run,
        ManualTimeProvider clock,
        List<SimulationClockAdvance> clockAdvances,
        CancellationTokenSource wallClock,
        CancellationToken callerToken)
    {
        // Every terminal outcome materializes any in-flight sub-workflow scopes first (§15-8a):
        // a stop/suspension/exhaustion inside a child unwinds before the invoking steps reach
        // their boundaries, and a goto transfer completes without one.
        SimulationOutcome Finish(SimulationOutcome outcome)
        {
            run.FinalizeInFlightScopes(outcome);
            return outcome;
        }

        int nextTrigger = 0;
        while (true)
        {
            WorkflowRunResultKind kind;
            try
            {
                // §15 8b: consume any overrides at the cursor first — an overridden step records as
                // skipped and the cursor advances, so the executor re-enters PAST it (the durable
                // Skip protocol replayed).
                run.SkipOverriddenSteps();
                run.BeginInvocation();
                kind = await loaded.Workflow.RunAsync(transports, null, workspace, scenario.Inputs, run, wallClock.Token).ConfigureAwait(false);
            }
            catch (SimulationSkipException)
            {
                continue; // the checkpoint met an overridden step: re-enter; SkipOverriddenSteps consumes it
            }
            catch (SimulationPauseException)
            {
                return Finish(SimulationOutcome.Paused);
            }
            catch (SimulationBudgetException)
            {
                return Finish(SimulationOutcome.BudgetExhausted);
            }
            catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
            {
                return Finish(SimulationOutcome.BudgetExhausted); // the wall-clock cap
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The executor met something the scenario (or the source contract) cannot express —
                // an unscripted status with no declared response, a bodiless response where content
                // is declared. That is a FAULT in the trace, never a simulator crash.
                await run.FaultAsync(run.CurrentStepId ?? "(executor)", 1, ex.Message, CancellationToken.None).ConfigureAwait(false);
                return Finish(SimulationOutcome.Faulted);
            }

            switch (kind)
            {
                case WorkflowRunResultKind.Completed:
                    return Finish(SimulationOutcome.Completed);
                case WorkflowRunResultKind.Faulted:
                    return Finish(SimulationOutcome.Faulted);
                default:
                    break;
            }

            WorkflowWait wait = run.RecordedWait ?? default;
            if (wait.Kind == WorkflowWaitKind.Timer)
            {
                if (!scenario.AutoAdvanceClock)
                {
                    return Finish(SimulationOutcome.Suspended);
                }

                clock.Advance(wait.DueAt);
                clockAdvances.Add(new SimulationClockAdvance(wait.DueAt, "timer due"));
                continue;
            }

            // A message wait: deliver the next matching trigger, or end suspended.
            int found = -1;
            for (int i = nextTrigger; i < scenario.Triggers.Count; i++)
            {
                SimulationTrigger candidate = scenario.Triggers[i];
                if (candidate.Channel == wait.Channel
                    && (wait.CorrelationId is null || candidate.CorrelationId is null || candidate.CorrelationId == wait.CorrelationId))
                {
                    found = i;
                    break;
                }
            }

            if (found < 0)
            {
                return Finish(SimulationOutcome.Suspended);
            }

            run.DeliverMessage(scenario.Triggers[found].Payload);
            nextTrigger = found + 1;
        }
    }

    /// <summary>
    /// Resolves the scenario's step-output overrides (§15 8b) against the document: for each
    /// overridden step, the provided outputs plus where the replay resumes after the skip. The step
    /// never executes, so there is no <c>$statusCode</c> to judge — only a criteria-less success
    /// action can route (end and goto honoured; anything else, or none, falls through to the next
    /// step), the same rule the mock applies.
    /// </summary>
    private static IReadOnlyDictionary<string, StepOutputOverride> BuildOverrides(WorkflowShape shape, SimulationScenario scenario)
    {
        if (scenario.StepOutputOverrides.Count == 0)
        {
            return System.Collections.ObjectModel.ReadOnlyDictionary<string, StepOutputOverride>.Empty;
        }

        var overrides = new Dictionary<string, StepOutputOverride>(StringComparer.Ordinal);
        for (int i = 0; i < shape.StepIds.Count; i++)
        {
            string stepId = shape.StepIds[i];
            if (!scenario.StepOutputOverrides.TryGetValue(stepId, out JsonElement provided))
            {
                continue;
            }

            WorkflowShape.Action? fired = null;
            IReadOnlyList<WorkflowShape.Action> actions =
                shape.Steps.TryGetValue(stepId, out WorkflowShape.Step? step) && step.OnSuccess.Count > 0
                    ? step.OnSuccess
                    : shape.WorkflowSuccessActions;
            foreach (WorkflowShape.Action action in actions)
            {
                if (action.Criteria.Count == 0)
                {
                    fired = action;
                    break;
                }
            }

            int next = i + 1;
            if (fired is { Type: "end" })
            {
                next = shape.StepIds.Count; // the terminal outputs state
            }
            else if (fired is { Type: "goto" })
            {
                int target = fired.Target is { } targetStep ? IndexOf(shape.StepIds, targetStep) : -1;
                next = target >= 0 ? target : shape.StepIds.Count; // an unresolvable target ends the run, as the mock's loop does
            }

            overrides[stepId] = new StepOutputOverride(
                provided,
                next,
                fired is null ? new SimulatedAction("fallThrough", null, null) : new SimulatedAction(fired.Type, fired.Name, fired.Target));
        }

        return overrides;
    }

    private static int IndexOf(IReadOnlyList<string> stepIds, string stepId)
    {
        for (int i = 0; i < stepIds.Count; i++)
        {
            if (string.Equals(stepIds[i], stepId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Post-hoc analysis: criterion truth tables and inferred routing, re-evaluated against the captured deterministic context.</summary>
    private static void AnalyzeTrace(in WorkflowShape shape, SimulationScenario scenario, SimulationResult result, List<IDisposable> owned)
        => AnalyzeSteps(shape, shape.Workflows, scenario.Inputs, result.Steps, result.Exchanges, owned);

    // Analyzes one trace level's records against ITS workflow's compiled criteria/actions, then
    // recurses into each record's nested sub-trace with the sub-workflow's own shape (§15-8a).
    // Child levels evaluate with UNDEFINED $inputs — the child's built inputs document does not
    // survive the invocation (declared v1 limit; $statusCode/$response criteria are unaffected).
    // Cross-source sub-workflows have no local shape and are recorded unanalyzed (§10 F10).
    private static void AnalyzeSteps(
        in WorkflowShape shape,
        IReadOnlyDictionary<string, WorkflowShape> workflows,
        JsonElement inputs,
        IReadOnlyList<SimulatedStepRecord> steps,
        IReadOnlyList<MockApiExchange> exchanges,
        List<IDisposable> owned)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            SimulatedStepRecord record = steps[i];
            if (record.SubTrace is { } sub && workflows.TryGetValue(sub.WorkflowId, out WorkflowShape? childShape))
            {
                AnalyzeSteps(childShape, workflows, default, sub.Steps, exchanges, owned);
            }

            if (record.Skipped)
            {
                // §15 8b: the step never executed — no exchange, no truth table; its routing
                // decision was fixed when the override was consumed. Its PROVIDED outputs still
                // feed later steps' contexts through the p-loop below.
                continue;
            }

            if (!shape.Steps.TryGetValue(record.StepId, out WorkflowShape.Step? step))
            {
                continue;
            }

            var context = new WorkflowExecutionContext();
            if (inputs.ValueKind is not JsonValueKind.Undefined)
            {
                context.SetInputs(inputs);
            }

            for (int p = 0; p < i; p++)
            {
                if (steps[p].Outputs.ValueKind is not JsonValueKind.Undefined)
                {
                    context.SetStepOutputs(steps[p].StepId, steps[p].Outputs);
                }
            }

            if (record.ExchangeCount > 0)
            {
                MockApiExchange last = exchanges[record.FirstExchange + record.ExchangeCount - 1];
                context.SetResponseStatusCode(last.StatusCode);
                if (!last.ResponseBody.IsEmpty)
                {
                    try
                    {
                        ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse(last.ResponseBody);
                        owned.Add(body);
                        context.SetResponseBody(body.RootElement);
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        // A non-JSON scripted body: criteria over $response.body simply see nothing.
                    }
                }
            }

            bool allPassed = true;
            if (step.SuccessCriteria.Count > 0)
            {
                var verdicts = new List<SimulatedCriterionVerdict>(step.SuccessCriteria.Count);
                foreach (WorkflowShape.Criterion criterion in step.SuccessCriteria)
                {
                    bool satisfied = Evaluate(criterion, context);
                    allPassed &= satisfied;
                    verdicts.Add(new SimulatedCriterionVerdict(criterion.Condition, satisfied));
                }

                record.SuccessCriteria = verdicts;
            }
            else if (record.ExchangeCount > 0)
            {
                // No explicit criteria: success is the transport's IsSuccess (2xx).
                MockApiExchange last = exchanges[record.FirstExchange + record.ExchangeCount - 1];
                allPassed = last.StatusCode is >= 200 and < 300;
            }

            if (record.Faulted)
            {
                record.ActionTaken = new SimulatedAction("fault", null, null);
                continue;
            }

            IReadOnlyList<WorkflowShape.Action> actions = allPassed
                ? (step.OnSuccess.Count > 0 ? step.OnSuccess : shape.WorkflowSuccessActions)
                : (step.OnFailure.Count > 0 ? step.OnFailure : shape.WorkflowFailureActions);
            WorkflowShape.Action? fired = null;
            foreach (WorkflowShape.Action action in actions)
            {
                bool matches = true;
                foreach (WorkflowShape.Criterion criterion in action.Criteria)
                {
                    matches &= Evaluate(criterion, context);
                }

                if (matches)
                {
                    fired = action;
                    break;
                }
            }

            record.ActionTaken = fired is null
                ? new SimulatedAction(allPassed ? "fallThrough" : "fault", null, null)
                : new SimulatedAction(fired.Type, fired.Name, fired.Target);
        }
    }

    private static bool Evaluate(in WorkflowShape.Criterion criterion, WorkflowExecutionContext context)
    {
        try
        {
            return CompiledCriterion.Compile(criterion.Type, criterion.Condition, criterion.ContextExpression).Evaluate(context);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            return false; // a criterion that cannot evaluate against this context is unsatisfied
        }
    }

    private LoadedWorkflow? GetOrCompile(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
    {
        string hash = ContentHash(workflowUtf8, sources);
        lock (this.cacheLock)
        {
            if (this.cache.TryGetValue(hash, out LoadedWorkflow? cached))
            {
                this.recency.Remove(hash);
                this.recency.AddFirst(hash);
                return cached;
            }
        }

        WorkflowExecutorArtifact? artifact = this.provider.BuildExecutor(workflowUtf8, sources, hash);
        if (artifact is not { } built)
        {
            return null;
        }

        lock (this.cacheLock)
        {
            if (this.cache.TryGetValue(hash, out LoadedWorkflow? raced))
            {
                return raced;
            }

            LoadedWorkflow loaded = this.loader.Load(hash, 0, built.Assembly, built.Manifest, hash);
            this.cache[hash] = loaded;
            this.recency.AddFirst(hash);
            while (this.recency.Count > this.maxCachedExecutors)
            {
                string evict = this.recency.Last!.Value;
                this.recency.RemoveLast();
                this.cache.Remove(evict);
                this.loader.Unload(evict, 0);
            }

            return loaded;
        }
    }

    private static string ContentHash(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(workflowUtf8.Span);
        foreach (KeyValuePair<string, byte[]> source in sources)
        {
            sha.AppendData(System.Text.Encoding.UTF8.GetBytes(source.Key));
            sha.AppendData(source.Value);
        }

        Span<byte> digest = stackalloc byte[32];
        sha.GetHashAndReset(digest);
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>The document facts the analysis needs, parsed once per command.</summary>
    private sealed class WorkflowShape
    {
        public IReadOnlyList<string> StepIds { get; private init; } = [];

        public IReadOnlyList<string> SourceNames { get; private init; } = [];

        public IReadOnlyDictionary<string, Step> Steps { get; private init; } = new Dictionary<string, Step>();

        public IReadOnlyList<Action> WorkflowSuccessActions { get; private init; } = [];

        public IReadOnlyList<Action> WorkflowFailureActions { get; private init; } = [];

        /// <summary>Gets every workflow's shape by workflowId (the whole document, the root included).</summary>
        public IReadOnlyDictionary<string, WorkflowShape> Workflows { get; private init; } = new Dictionary<string, WorkflowShape>(StringComparer.Ordinal);

        public static WorkflowShape From(JsonElement root)
        {
            var sourceNames = new List<string>();
            if (root.TryGetProperty("sourceDescriptions"u8, out JsonElement sourceDescriptions) && sourceDescriptions.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement source in sourceDescriptions.EnumerateArray())
                {
                    if (source.TryGetProperty("name"u8, out JsonElement name) && name.GetString() is { Length: > 0 } value)
                    {
                        sourceNames.Add(value);
                    }
                }
            }

            JsonElement components = default;
            root.TryGetProperty("components"u8, out components);

            // Every workflow in the document gets its own shape, keyed by workflowId — the child
            // step ids drive sub-workflow scopes (§15-8a), and the per-workflow criteria/actions
            // drive scoped-stop validation and nested trace analysis. The FIRST workflow is the
            // simulated one; its shape is the returned root (carrying the map).
            var byId = new Dictionary<string, WorkflowShape>(StringComparer.Ordinal);
            List<string>? firstStepIds = null;
            Dictionary<string, Step>? firstSteps = null;
            IReadOnlyList<Action> firstSuccess = [];
            IReadOnlyList<Action> firstFailure = [];
            if (root.TryGetProperty("workflows"u8, out JsonElement workflows) && workflows.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement workflow in workflows.EnumerateArray())
                {
                    IReadOnlyList<Action> workflowSuccess = ReadActions(workflow, "successActions"u8, components);
                    IReadOnlyList<Action> workflowFailure = ReadActions(workflow, "failureActions"u8, components);
                    var stepIds = new List<string>();
                    var steps = new Dictionary<string, Step>(StringComparer.Ordinal);
                    if (workflow.TryGetProperty("steps"u8, out JsonElement stepArray) && stepArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement step in stepArray.EnumerateArray())
                        {
                            if (!step.TryGetProperty("stepId"u8, out JsonElement id) || id.GetString() is not { Length: > 0 } stepId)
                            {
                                continue;
                            }

                            stepIds.Add(stepId);
                            steps[stepId] = new Step(
                                ReadCriteria(step, "successCriteria"u8, components),
                                ReadActions(step, "onSuccess"u8, components),
                                ReadActions(step, "onFailure"u8, components));
                        }
                    }

                    if (workflow.TryGetProperty("workflowId"u8, out JsonElement workflowId) && workflowId.GetString() is { Length: > 0 } wid)
                    {
                        byId[wid] = new WorkflowShape
                        {
                            StepIds = stepIds,
                            SourceNames = sourceNames,
                            Steps = steps,
                            WorkflowSuccessActions = workflowSuccess,
                            WorkflowFailureActions = workflowFailure,
                        };
                    }

                    if (firstStepIds is null)
                    {
                        firstStepIds = stepIds;
                        firstSteps = steps;
                        firstSuccess = workflowSuccess;
                        firstFailure = workflowFailure;
                    }
                }
            }

            return new WorkflowShape
            {
                StepIds = firstStepIds ?? [],
                SourceNames = sourceNames,
                Steps = firstSteps ?? new Dictionary<string, Step>(StringComparer.Ordinal),
                WorkflowSuccessActions = firstSuccess,
                WorkflowFailureActions = firstFailure,
                Workflows = byId,
            };
        }

        private static IReadOnlyList<Criterion> ReadCriteria(JsonElement owner, ReadOnlySpan<byte> property, JsonElement components)
        {
            if (!owner.TryGetProperty(property, out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var criteria = new List<Criterion>();
            foreach (JsonElement item in list.EnumerateArray())
            {
                if (TryReadCriterion(item, out Criterion criterion))
                {
                    criteria.Add(criterion);
                }
            }

            return criteria;
        }

        private static bool TryReadCriterion(JsonElement item, out Criterion criterion)
        {
            criterion = default;
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("condition"u8, out JsonElement condition)
                || condition.GetString() is not { Length: > 0 } text)
            {
                return false;
            }

            string? type = null;
            if (item.TryGetProperty("type"u8, out JsonElement typeElement))
            {
                type = typeElement.ValueKind == JsonValueKind.String
                    ? typeElement.GetString()
                    : typeElement.TryGetProperty("type"u8, out JsonElement innerType) ? innerType.GetString() : null;
            }

            string? contextExpression = item.TryGetProperty("context"u8, out JsonElement contextElement) ? contextElement.GetString() : null;
            criterion = new Criterion(
                text,
                type switch { "regex" => CriterionType.Regex, "jsonpath" => CriterionType.JsonPath, _ => CriterionType.Simple },
                contextExpression);
            return true;
        }

        private static IReadOnlyList<Action> ReadActions(JsonElement owner, ReadOnlySpan<byte> property, JsonElement components)
        {
            if (!owner.TryGetProperty(property, out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var actions = new List<Action>();
            foreach (JsonElement item in list.EnumerateArray())
            {
                JsonElement action = item;
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("reference"u8, out JsonElement reference))
                {
                    // $components.successActions.<name> / $components.failureActions.<name>
                    if (!TryResolveComponentAction(reference, components, out action))
                    {
                        continue;
                    }
                }

                if (action.ValueKind != JsonValueKind.Object || !action.TryGetProperty("type"u8, out JsonElement type)
                    || type.GetString() is not { Length: > 0 } typeName)
                {
                    continue;
                }

                var criteria = new List<Criterion>();
                if (action.TryGetProperty("criteria"u8, out JsonElement criteriaList) && criteriaList.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement criterionElement in criteriaList.EnumerateArray())
                    {
                        if (TryReadCriterion(criterionElement, out Criterion criterion))
                        {
                            criteria.Add(criterion);
                        }
                    }
                }

                string? name = action.TryGetProperty("name"u8, out JsonElement nameElement) ? nameElement.GetString() : null;
                string? target = action.TryGetProperty("stepId"u8, out JsonElement stepTarget) ? stepTarget.GetString()
                    : action.TryGetProperty("workflowId"u8, out JsonElement workflowTarget) ? workflowTarget.GetString() : null;
                actions.Add(new Action(typeName, name, target, criteria));
            }

            return actions;
        }

        private static bool TryResolveComponentAction(JsonElement reference, JsonElement components, out JsonElement action)
        {
            action = default;
            if (components.ValueKind != JsonValueKind.Object || reference.GetString() is not { } expression)
            {
                return false;
            }

            string[] segments = expression.Split('.');
            if (segments.Length != 3 || segments[0] != "$components")
            {
                return false;
            }

            return components.TryGetProperty(segments[1], out JsonElement kind)
                && kind.ValueKind == JsonValueKind.Object
                && kind.TryGetProperty(segments[2], out action);
        }

        public sealed record Step(
            IReadOnlyList<Criterion> SuccessCriteria,
            IReadOnlyList<Action> OnSuccess,
            IReadOnlyList<Action> OnFailure);

        public sealed record Action(string Type, string? Name, string? Target, IReadOnlyList<Criterion> Criteria);

        public readonly record struct Criterion(string Condition, CriterionType Type, string? ContextExpression);
    }

    private sealed class CompositeDisposable(List<IDisposable> owned) : IDisposable
    {
        public void Dispose()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                owned[i].Dispose();
            }
        }
    }
}