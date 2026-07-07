// <copyright file="DraftWorkflowResumer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Resolves and runs §18 draft runs on the runner (workflow-designer design §18, staging item 3): a run carrying
/// the reserved <see cref="DraftRuns.RunWorkflowId"/> is resolved from the <see cref="IDraftRunStore"/> — the
/// captured document + sources are compiled through the durable <see cref="IWorkflowExecutorProvider"/> with a
/// bounded content-hash cache (one Roslyn compile per captured document state, however many runs and resumes
/// replay it — the same discipline as the simulator's compile cache) — and driven through the same transport
/// binder and <see cref="IHostedWorkflow.RunAsync"/> path a catalog run takes. Any other run is delegated to the
/// inner resumer (typically a <see cref="HostedWorkflowResumer"/>), which stays byte-for-byte untouched.
/// </summary>
/// <remarks>
/// Because the transports come from the same <see cref="WorkflowTransportBinder"/> the host installs for catalog
/// runs (the §13 credential-cache binder), a draft run resolves the environment's credential bindings on the
/// runner, as the runner's identity — no secret ever reaches the control plane, the designer, or the capture.
/// The resumer owns its executor loader; dispose it to unload the compiled drafts.
/// </remarks>
public sealed class DraftWorkflowResumer : IDisposable
{
    private readonly IDraftRunStore drafts;
    private readonly IWorkflowExecutorProvider provider;
    private readonly WorkflowTransportBinder transportBinder;
    private readonly WorkflowResumer? inner;
    private readonly WorkflowExecutorLoader loader = new();
    private readonly Dictionary<string, LoadedWorkflow> cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> recency = new();
    private readonly int maxCachedExecutors;
    private readonly Lock cacheLock = new();

    /// <summary>Initializes a new instance of the <see cref="DraftWorkflowResumer"/> class.</summary>
    /// <param name="drafts">The draft-run store captures are resolved from.</param>
    /// <param name="provider">The executor provider that compiles a captured draft (MUST be a durable-mode provider — the checkpoint seam is the run's durability).</param>
    /// <param name="transportBinder">Binds the compiled draft's descriptor to the transports the run executes through — install the same credential-aware binder the host uses for catalog runs.</param>
    /// <param name="inner">The resumer non-draft runs are delegated to (typically <see cref="HostedWorkflowResumer.AsResumer"/>), or <see langword="null"/> for a draft-only runner.</param>
    /// <param name="maxCachedExecutors">How many compiled captures stay loaded (least-recently-used beyond this).</param>
    public DraftWorkflowResumer(
        IDraftRunStore drafts,
        IWorkflowExecutorProvider provider,
        WorkflowTransportBinder transportBinder,
        WorkflowResumer? inner = null,
        int maxCachedExecutors = 16)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(transportBinder);
        this.drafts = drafts;
        this.provider = provider;
        this.transportBinder = transportBinder;
        this.inner = inner;
        this.maxCachedExecutors = maxCachedExecutors;
    }

    /// <summary>Gets this resumer as the <see cref="WorkflowResumer"/> delegate the dispatcher, worker, and management client consume.</summary>
    /// <returns>The resumer delegate.</returns>
    public WorkflowResumer AsResumer() => this.ResumeAsync;

    /// <summary>
    /// Resolves a draft run's captured draft, compiles it (cache-hit for a repeat), binds its transports, and
    /// starts or resumes the durable run; a non-draft run is delegated to the inner resumer.
    /// </summary>
    /// <param name="run">The run to start (fresh) or resume (restored checkpoint).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run outcome.</returns>
    public async ValueTask<WorkflowRunResultKind> ResumeAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!DraftRuns.IsDraftRun(run.WorkflowId))
        {
            return this.inner is { } resume
                ? await resume(run, cancellationToken).ConfigureAwait(false)
                : throw new InvalidOperationException($"Run '{run.Id.Value}' executes workflow '{run.WorkflowId}', which this draft-only resumer does not host.");
        }

        IHostedWorkflow hosted = await this.ResolveAsync(run.Id, cancellationToken).ConfigureAwait(false);
        WorkflowTransports transports = this.transportBinder(hosted.Descriptor, run.SecurityTags);

        using JsonWorkspace workspace = JsonWorkspace.Create();
        try
        {
            return await hosted.RunAsync(transports.ApiTransports, transports.MessageTransport, workspace, run.Inputs, run, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (IApiTransport apiTransport in transports.ApiTransports.Values)
            {
                await apiTransport.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() => this.loader.Dispose();

    private async ValueTask<IHostedWorkflow> ResolveAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        // The capture record carries the content hash, so a cache hit needs no package read.
        string contentHash;
        using (ParsedJsonDocument<DraftRun>? record = await this.drafts.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Draft run '{id.Value}' has no captured draft in the draft-run store."))
        {
            contentHash = record.RootElement.ContentHashValue;
        }

        lock (this.cacheLock)
        {
            if (this.cache.TryGetValue(contentHash, out LoadedWorkflow? cached))
            {
                this.recency.Remove(contentHash);
                this.recency.AddFirst(contentHash);
                return cached.Workflow;
            }
        }

        ReadOnlyMemory<byte> package = await this.drafts.GetPackageAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Draft run '{id.Value}' has a capture record but no captured package.");
        WorkflowPackageContents contents = WorkflowPackage.Open(package);

        WorkflowExecutorArtifact? artifact = this.provider.BuildExecutor(contents.Workflow, contents.Sources, contentHash);
        if (artifact is not { } built)
        {
            throw new InvalidOperationException($"Draft run '{id.Value}' is not executable: the captured document did not compile.");
        }

        lock (this.cacheLock)
        {
            if (this.cache.TryGetValue(contentHash, out LoadedWorkflow? raced))
            {
                return raced.Workflow;
            }

            LoadedWorkflow loaded = this.loader.Load(contentHash, 0, built.Assembly, built.Manifest, contentHash);
            this.cache[contentHash] = loaded;
            this.recency.AddFirst(contentHash);
            while (this.recency.Count > this.maxCachedExecutors)
            {
                string evict = this.recency.Last!.Value;
                this.recency.RemoveLast();
                this.cache.Remove(evict);
                this.loader.Unload(evict, 0);
            }

            return loaded.Workflow;
        }
    }
}