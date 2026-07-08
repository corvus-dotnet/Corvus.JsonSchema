// <copyright file="InProcessDraftRunner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A composable, environment-pinned runner that hosts §18 <c>$draft</c> debug runs in the <em>same</em> process
/// as the control plane (workflow-designer design §18 slice 3e-2b). A single-process deployment — the live demo,
/// the container-free tests — advances draft runs with no separate runner process by pumping this runner; a
/// multi-process deployment runs the same composition out of process instead. It <em>composes</em> the existing
/// dispatch, resume, and trace-assembly pieces and adds nothing to the durable machinery: no handler change, no
/// change to the persisted checkpoint shape, no store fan-out.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it composes.</b> A <see cref="WorkflowDispatcher"/> pinned to <c>runnerEnvironment</c> claims the next
/// batch of Pending/orphaned <c>$draft</c> runs (the reserved <see cref="DraftRuns.RunWorkflowId"/>); a
/// <see cref="DraftWorkflowResumer"/> resolves each claimed run's captured draft, compiles it (its content-hash
/// LRU cache), binds its transports, and drives it through the same <see cref="IHostedWorkflow.RunAsync"/> path a
/// catalog run takes (catching a §18 debugger <c>WorkflowPauseException</c> → Suspended, from slice 3e-1); an
/// optional <see cref="WorkflowWorker"/> resumes due timer waits through the same resumer. The runner wraps each
/// per-run credentialed API transport the binder yields (from <c>SourceCredentialTransports.CreateBinder</c> in
/// production, or a caller-supplied binder in tests) in a <see cref="RecordingApiTransport"/>, so every run's
/// metadata-only exchanges (method, pre-auth path, status — never a body) are recorded, and assembles the
/// <c>SimulationTrace</c>-shaped metadata trace (<see cref="MetadataTraceAssembler"/>) from the run's
/// just-persisted checkpoint plus that run's recorded exchanges.
/// </para>
/// <para>
/// <b>Correlating a run with its exchanges.</b> The binder the <see cref="DraftWorkflowResumer"/> is constructed
/// with is shared across every run, is invoked per run with only the descriptor and the run's tags (never the run
/// id), and is invoked <em>after</em> an <c>await</c> inside the resume — so it cannot itself key recorders by run.
/// Rather than widen <see cref="DraftWorkflowResumer"/>'s surface, the correlation is done one level out, at the
/// <see cref="WorkflowResumer"/> delegate the dispatcher and worker actually call: that delegate establishes a
/// per-run <see cref="DraftRunRecording"/> in an <see cref="AsyncLocal{T}"/> and wires the run's
/// <see cref="WorkflowRun.OnCheckpointed"/> hook to its step-boundary marker, drives the resume (the shared recording binder reads
/// the ambient recording and wraps each source's transport in a <see cref="RecordingApiTransport"/> that appends to
/// it (<see cref="AsyncLocal{T}"/> flows down through the awaited resume and never leaks up to the dispatcher, so
/// sequential and even concurrent pumps stay isolated), then assembles the trace from the run plus that recording
/// and persists it to the trace store.
/// </para>
/// <para>
/// <b>The trace is persisted durably (§18 R4).</b> Each run's assembled metadata trace is written to the injected
/// <see cref="IDraftRunTraceStore"/>, keyed by run id, so a control plane in a <em>different</em> process reads it
/// (the multi-process debug-run topology). In a single-process deployment the trace store is the in-memory one, so
/// the same code path serves the live demo and the container-free tests
/// — there is no separate in-process trace cache.
/// </para>
/// <para>
/// <b>Pumping.</b> <see cref="RunPendingAsync"/> is the pump: it claims and advances one batch and is independently
/// callable, so a test drives it synchronously. <see cref="Start"/> layers an optional self-driving background loop
/// (a plain cancellable <see cref="Task"/> at a poll interval; there is no <c>IHostedService</c> precedent in this
/// codebase, so the runner takes no ASP.NET hosting dependency — the host starts and stops the loop) over the same
/// pump.
/// </para>
/// </remarks>
public sealed class InProcessDraftRunner : IAsyncDisposable
{
    private static readonly string[] DraftHosting = [DraftRuns.RunWorkflowId];

    private readonly IWorkflowStateStore store;
    private readonly IDraftRunTraceStore traceStore;
    private readonly WorkflowTransportBinder baseBinder;
    private readonly DraftWorkflowResumer resumer;
    private readonly WorkflowDispatcher dispatcher;
    private readonly WorkflowWorker? worker;
    private readonly WorkflowResumer tracingResumer;
    private readonly AsyncLocal<DraftRunRecording?> currentRecording = new();
    private readonly Lock loopGate = new();
    private CancellationTokenSource? loopCts;
    private Task? loop;

    /// <summary>Initializes a new instance of the <see cref="InProcessDraftRunner"/> class.</summary>
    /// <param name="store">The state store the draft runs are claimed from (must implement
    /// <see cref="IWorkflowDispatchIndex"/>, and <see cref="IWorkflowWaitIndex"/> when <paramref name="hostTimerWaits"/>
    /// is set).</param>
    /// <param name="owner">This runner's opaque identity, used as the dispatcher's and worker's lease owner.</param>
    /// <param name="runnerEnvironment">The single deployment environment this runner serves (design §5.5/§18); draft
    /// dispatch is fail-closed and requires it, so it must be non-empty.</param>
    /// <param name="drafts">The draft-run store the captured drafts are resolved from.</param>
    /// <param name="traceStore">The durable trace store the runner persists each run's assembled metadata trace to
    /// (§18 R2), so a control plane in a <em>different</em> process reads it. In a single-process deployment this is
    /// the in-memory trace store, so the same code path serves both.</param>
    /// <param name="provider">The durable-mode executor provider that compiles a captured draft.</param>
    /// <param name="binder">The per-run transport binder each run executes through — the credential-aware
    /// <c>SourceCredentialTransports.CreateBinder</c> binder in production, or a caller-supplied binder in tests. The
    /// runner wraps each API transport it yields in a <see cref="RecordingApiTransport"/>.</param>
    /// <param name="inner">The resumer non-draft runs are delegated to, or <see langword="null"/> for a draft-only
    /// runner (the usual in-process draft-hosting case).</param>
    /// <param name="hostTimerWaits">Whether to compose a <see cref="WorkflowWorker"/> so <see cref="RunPendingAsync"/>
    /// also resumes due timer waits (a draft run that suspended on a retry timer). Message-wait delivery is not driven
    /// by the pump; a host that needs it calls the worker's <see cref="WorkflowWorker.DeliverMessageAsync"/> directly.</param>
    /// <param name="timeProvider">The time source for lease TTLs and due-timer evaluation; defaults to
    /// <see cref="TimeProvider.System"/>.</param>
    /// <param name="maxCachedExecutors">How many compiled captures the resumer keeps loaded.</param>
    public InProcessDraftRunner(
        IWorkflowStateStore store,
        string owner,
        string runnerEnvironment,
        IDraftRunStore drafts,
        IDraftRunTraceStore traceStore,
        IWorkflowExecutorProvider provider,
        WorkflowTransportBinder binder,
        WorkflowResumer? inner = null,
        bool hostTimerWaits = false,
        TimeProvider? timeProvider = null,
        int maxCachedExecutors = 16)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(owner);
        ArgumentException.ThrowIfNullOrEmpty(runnerEnvironment);
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(traceStore);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(binder);

        this.store = store;
        this.traceStore = traceStore;
        this.baseBinder = binder;
        this.resumer = new DraftWorkflowResumer(drafts, provider, this.RecordingBinder, inner, maxCachedExecutors);
        this.dispatcher = new WorkflowDispatcher(store, owner, timeProvider, runnerEnvironment: runnerEnvironment);
        this.worker = hostTimerWaits ? new WorkflowWorker(store, owner, timeProvider) : null;
        this.tracingResumer = this.RunAndTraceAsync;
    }

    /// <summary>
    /// Claims and advances the next batch of <c>$draft</c> runs pinned to this runner's environment — each claimed
    /// run's exchanges recorded and its metadata trace assembled and cached — and, when a worker is composed, resumes
    /// every due timer wait the same way.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs advanced this cycle (dispatched fresh/orphaned runs plus, if configured, timer resumes).</returns>
    public async ValueTask<int> RunPendingAsync(CancellationToken cancellationToken = default)
    {
        int advanced = await this.dispatcher.DispatchClaimableAsync(DraftHosting, this.tracingResumer, cancellationToken).ConfigureAwait(false);
        if (this.worker is { } timerWorker)
        {
            advanced += await timerWorker.ResumeDueTimersAsync(this.tracingResumer, cancellationToken).ConfigureAwait(false);
        }

        return advanced;
    }

    /// <summary>
    /// Gets this runner's recording-and-tracing <see cref="WorkflowResumer"/> — the exact delegate <see cref="RunPendingAsync"/>
    /// drives each claimed run through. A single-process host that needs to advance one specific run <em>interactively</em>
    /// (the §18 debug-run stepper, which sets a per-advance <see cref="WorkflowRun.SetPause"/> before it drives, and so
    /// cannot go through the dispatcher's internally-constructed run) loads the run itself and awaits this delegate: it
    /// records the run's metadata-only exchanges, resumes it (honouring any pause), and assembles+caches its metadata
    /// trace exactly as a pumped run — so <see cref="TryGetTrace"/> answers for it afterwards. It is also the resumer a
    /// <c>SecuredWorkflowManagement</c> is constructed with so its native faulted-run resume (retry/skip/rewind/patch)
    /// records+traces through the same path. This exposes existing behaviour; it drives no dispatch, lease, or claim.
    /// </summary>
    public WorkflowResumer Resumer => this.tracingResumer;

    /// <summary>
    /// Starts the optional self-driving background loop: it pumps <see cref="RunPendingAsync"/> every
    /// <paramref name="pollInterval"/> until <see cref="StopAsync"/> (or disposal). The pump stays independently
    /// callable, so a host may either start this loop or drive <see cref="RunPendingAsync"/> itself, but not usefully
    /// both at once.
    /// </summary>
    /// <param name="pollInterval">How long to wait between pumps; defaults to one second.</param>
    /// <param name="onError">An optional callback invoked with any non-cancellation exception a pump throws, so the loop
    /// surfaces failures instead of swallowing them silently while it keeps polling; <see langword="null"/> ignores them.</param>
    /// <exception cref="InvalidOperationException">The loop is already running.</exception>
    public void Start(TimeSpan? pollInterval = null, Action<Exception>? onError = null)
    {
        lock (this.loopGate)
        {
            if (this.loop is not null)
            {
                throw new InvalidOperationException("The draft runner's background loop is already running.");
            }

            TimeSpan interval = pollInterval ?? TimeSpan.FromSeconds(1);
            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            this.loopCts = cts;
            this.loop = Task.Run(() => this.PollLoopAsync(interval, onError, token), CancellationToken.None);
        }
    }

    /// <summary>Stops the background loop started by <see cref="Start"/> and waits for it to drain; a no-op if it is not running.</summary>
    /// <returns>A task that completes when the loop has stopped.</returns>
    public async ValueTask StopAsync()
    {
        Task? running;
        CancellationTokenSource? cts;
        lock (this.loopGate)
        {
            running = this.loop;
            cts = this.loopCts;
            this.loop = null;
            this.loopCts = null;
        }

        if (cts is null)
        {
            return;
        }

        await cts.CancelAsync().ConfigureAwait(false);
        try
        {
            if (running is not null)
            {
                await running.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The loop observed the stop signal.
        }
        finally
        {
            cts.Dispose();
        }
    }

    /// <summary>Stops the background loop and unloads the compiled captured drafts. The ephemeral trace cache is dropped.</summary>
    /// <returns>A task that completes when the runner has stopped and released its resources.</returns>
    public async ValueTask DisposeAsync()
    {
        await this.StopAsync().ConfigureAwait(false);
        this.resumer.Dispose();
    }

    private async Task PollLoopAsync(TimeSpan interval, Action<Exception>? onError, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await this.RunPendingAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }

            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // The WorkflowResumer the dispatcher and worker call: it scopes a per-run recording sink into the AsyncLocal the
    // shared recording binder reads, drives the draft resume, then assembles and caches this run's metadata trace from
    // its just-persisted checkpoint plus exactly the recorders bound for it.
    private async ValueTask<WorkflowRunResultKind> RunAndTraceAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        var recording = new DraftRunRecording();
        this.currentRecording.Value = recording;

        // §18 R3: mark a step boundary at each durable checkpoint, so the assembler attributes exchanges to steps by
        // range (faithful across a step's retries). Always a debug run here, so the hook is always wired.
        run.OnCheckpointed = recording.MarkStepBoundary;
        try
        {
            WorkflowRunResultKind kind = await this.resumer.ResumeAsync(run, cancellationToken).ConfigureAwait(false);
            await this.AssembleTraceAsync(run.Id, recording, cancellationToken).ConfigureAwait(false);
            return kind;
        }
        finally
        {
            run.OnCheckpointed = null;
            this.currentRecording.Value = null;
        }
    }

    // The shared binder the DraftWorkflowResumer is constructed with: it wraps every API transport the base binder
    // yields in a RecordingApiTransport that appends to the ambient per-run recording (established by RunAndTraceAsync),
    // so the run's exchanges keep global call order across sources and are reachable once the resume returns. The
    // message transport is passed through untouched — its lifetime is the binder's, not the resumer's, and it is not
    // recorded here.
    private WorkflowTransports RecordingBinder(WorkflowDescriptor descriptor, SecurityTagSet runTags)
    {
        WorkflowTransports bound = this.baseBinder(descriptor, runTags);
        DraftRunRecording? recording = this.currentRecording.Value;
        if (recording is null)
        {
            // The binder was invoked outside a traced resume (no ambient recording); forward the transports untouched.
            return bound;
        }

        var recordingTransports = new Dictionary<string, IApiTransport>(bound.ApiTransports.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, IApiTransport> pair in bound.ApiTransports)
        {
            recordingTransports[pair.Key] = new RecordingApiTransport(pair.Value, recording);
        }

        return new WorkflowTransports(recordingTransports, bound.MessageTransport);
    }

    private async ValueTask AssembleTraceAsync(WorkflowRunId id, DraftRunRecording recording, CancellationToken cancellationToken)
    {
        // Attribute the ordered exchanges to the executed steps by the per-step boundaries the checkpoint hook marked
        // (§18 R3), then persist the trace durably so a control plane in a different process reads it (§18 R4).
        IReadOnlyList<RecordedApiExchange> exchanges = recording.Exchanges;
        IReadOnlyList<int> stepBoundaries = recording.StepBoundaries;

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            await MetadataTraceAssembler.WriteTraceAsync(writer, this.store, id, exchanges, pausedBeforeStepId: null, stepBoundaries: stepBoundaries, cancellationToken: cancellationToken).ConfigureAwait(false);
            writer.Flush();
        }

        await this.traceStore.PutAsync(id, buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
    }
}