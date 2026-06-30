// <copyright file="DemoData.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.AsyncApi.Testing;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo;

/// <summary>
/// Seeds the demo catalog and runs from real packages (an Arazzo workflow + its OpenAPI sources), so the
/// browsable demo exercises the genuine metadata generator and JSON Schema validator — including the
/// composite shapes (a discriminated <c>oneOf</c> union, a <c>prefixItems</c> tuple, an
/// <c>additionalProperties</c> map) that the typed output editor renders and the <c>/validate</c> endpoint checks.
/// </summary>
public static class DemoData
{
    private static readonly CatalogOwner OnboardingOwner = new("Onboarding Team", "onboarding@example.com", "Identity", "https://runbooks.example.com/onboard");
    private static readonly CatalogOwner ReconcileOwner = new("Reconciliation Team", "reconcile@example.com", "Platform", "https://runbooks.example.com/nightly-reconcile");

    /// <summary>Builds the live <see cref="WorkflowResumer"/> that re-enters a run's compiled executor (§5/§8): it
    /// resolves the run's catalogued executor through the loader and runs it against transports that call this host's
    /// own /svc backends. <paramref name="baseUrlProvider"/> yields the host's base URL once it has started listening
    /// (the resumer is built before the server binds, but invoked only after).</summary>
    /// <param name="catalog">The catalog store the baked executor is loaded from.</param>
    /// <param name="baseUrlProvider">Yields this host's base URL (resolved after it starts listening).</param>
    /// <returns>The resumer delegate.</returns>
    public static WorkflowResumer CreateLiveResumer(IWorkflowCatalogStore catalog, Func<string> baseUrlProvider)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(baseUrlProvider);

        // One client per source, each rooted at the host with a handler that prefixes the source's /svc base path —
        // the generated client emits absolute operation paths (e.g. /accounts), so rooting at the host + prefixing in
        // a handler routes them to /svc/<source>/... (a base address WITH a path is dropped by absolute-path resolution).
        var clients = new ConcurrentDictionary<string, HttpClient>(StringComparer.Ordinal);
        HttpClient ClientFor(string source) => clients.GetOrAdd(source, s => new HttpClient(new SvcPrefixHandler($"/svc/{s}") { InnerHandler = new HttpClientHandler() })
        {
            BaseAddress = new Uri(baseUrlProvider()),
        });

        // A workflow with an AsyncAPI receive step needs an IMessageTransport supplied to its executor even though
        // the durable suspend/resume itself flows through the worker (WorkflowWorker.DeliverMessageAsync); the
        // in-process transport satisfies that and would also serve any Tier-1 blocking receive. Shared across runs.
        var messageTransport = new InMemoryMessageTransport();

        var resumer = new HostedWorkflowResumer(
            catalog,
            new WorkflowExecutorLoader(),
            (descriptor, runTags) => new WorkflowTransports(
                descriptor.Sources.ToDictionary(
                    source => source,
                    source => (IApiTransport)new HttpClientApiTransportFactory(ClientFor(source)).CreateTransport(),
                    StringComparer.Ordinal),
                descriptor.NeedsMessageTransport ? messageTransport : null));
        return resumer.AsResumer();
    }

    /// <summary>Executes fresh onboarding runs live, to demonstrate real execution: each creates a Pending run with
    /// inputs and drives it through the live resumer (which calls the hosted /svc/onboarding backend), so the browsable
    /// demo shows genuinely-executed runs rather than hand-seeded states. Two runs are executed: a standard applicant
    /// who clears the KYC score threshold and completes all four steps, and a sanctioned applicant who scores below it
    /// and faults live at the verifyIdentity success criterion. Best-effort — a failure is logged, not fatal.</summary>
    /// <param name="runStore">The run state store.</param>
    /// <param name="resumer">The live resumer (from <see cref="CreateLiveResumer"/>).</param>
    /// <param name="log">An optional sink for the outcome line.</param>
    /// <param name="timeProvider">The time provider (defaults to the system clock).</param>
    public static async ValueTask RunLiveOnboardingAsync(IWorkflowStateStore runStore, WorkflowResumer resumer, Action<string>? log = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(resumer);
        TimeProvider time = timeProvider ?? TimeProvider.System;

        // A standard applicant clears the KYC score threshold → the run completes all four steps.
        await RunLiveAsync(runStore, resumer, time, "run-onb-live01", "live01", """{"email":"ada@example.com","fullName":"Ada Lovelace","plan":"pro"}""", log).ConfigureAwait(false);

        // The same sanctioned applicant on v1 scores below the threshold and — because v1 does not handle the
        // failure — faults live at verifyIdentity. Nothing is hand-seeded: the success criterion is evaluated
        // against the real backend response. This intentionally-unhandled fault demonstrates how a failing step
        // surfaces in the control plane (the dev-test debugging experience), not a recommended design.
        await RunLiveAsync(runStore, resumer, time, "run-onb-live02", "live02", """{"email":"mallory@example.com","fullName":"Mallory Sanction","plan":"free"}""", log).ConfigureAwait(false);

        // The resilient v2 of the workflow HANDLES the same KYC failure: verifyIdentity's onFailure routes to the
        // applicant-notification step (skipping provisioning), so the run completes via the remediation branch
        // instead of faulting. Same applicant as live02, different (fixed) workflow version — the production design.
        await RunLiveAsync(runStore, resumer, time, "run-onb-live03", "live03", """{"email":"mallory@example.com","fullName":"Mallory Sanction","plan":"free"}""", log, "onboard-customer-v2").ConfigureAwait(false);

        // An asynchronous onboarding: the run suspends durably awaiting an out-of-band KYC verdict on the
        // kyc.verdict channel, then a delivered verdict message resumes it to completion — live suspend + resume.
        await RunLiveSuspendResumeAsync(runStore, resumer, time, "run-onb-live04", "live04", log).ConfigureAwait(false);

        // A resilient onboarding: the identity provider returns a transient incomplete result on the first check,
        // so the step retries with a backoff — the run suspends on a durable TIMER between attempts and resumes
        // when the backoff elapses, succeeding on the retry. Live timer suspend + resume.
        await RunLiveTimerResumeAsync(runStore, resumer, time, "run-onb-live05", "live05", log).ConfigureAwait(false);
    }

    private static async ValueTask RunLiveTimerResumeAsync(IWorkflowStateStore runStore, WorkflowResumer resumer, TimeProvider time, string runId, string correlationId, Action<string>? log)
    {
        try
        {
            // First leg: the identity check fails (transient incomplete result), so the step schedules a retry with
            // a backoff and the run suspends on a durable timer (returns Suspended).
            using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"email":"katherine@example.com","fullName":"Katherine Transient","plan":"pro"}"""u8.ToArray());
            using WorkflowRun run = WorkflowRun.CreateNew(runStore, runId, "onboard-customer-retry-v1", inputs.RootElement, time, correlationId: correlationId, tags: TagSet.FromTags(["tenant-7"]));
            WorkflowRunResultKind first = await resumer(run, default).ConfigureAwait(false);
            log?.Invoke($"Live retry onboarding run '{runId}' first leg: {first} (identity check backing off, retry on a durable timer).");

            // Wait for the backoff to elapse, then drive due timers — which wakes and resumes the run; the retried
            // identity check now succeeds, so the run completes.
            await Task.Delay(TimeSpan.FromSeconds(3), time).ConfigureAwait(false);
            var worker = new WorkflowWorker(runStore, "demo", time);
            int resumed = await worker.ResumeDueTimersAsync(resumer, default).ConfigureAwait(false);
            log?.Invoke($"Live retry onboarding run '{runId}': backoff elapsed, resumed {resumed} due timer(s) to completion.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"Live retry onboarding run '{runId}' failed: {ex}");
        }
    }

    private static async ValueTask RunLiveSuspendResumeAsync(IWorkflowStateStore runStore, WorkflowResumer resumer, TimeProvider time, string runId, string correlationId, Action<string>? log)
    {
        try
        {
            // First leg: create the account, then suspend at the awaitKycVerdict receive step (returns Suspended).
            using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"email":"grace@example.com","fullName":"Grace Hopper","plan":"enterprise"}"""u8.ToArray());
            using WorkflowRun run = WorkflowRun.CreateNew(runStore, runId, "onboard-customer-async-v1", inputs.RootElement, time, correlationId: correlationId, tags: TagSet.FromTags(["tenant-7"]));
            WorkflowRunResultKind first = await resumer(run, default).ConfigureAwait(false);
            log?.Invoke($"Live async onboarding run '{runId}' first leg: {first} (awaiting kyc.verdict message).");

            // The KYC provider posts its verdict out-of-band → deliver it on the channel, which wakes and resumes
            // the suspended run through the same live resumer, driving it to completion.
            using ParsedJsonDocument<JsonElement> verdict = ParsedJsonDocument<JsonElement>.Parse("""{"accountId":"3f2504e0-4f89-41d3-9a0c-0305e82c3301","verified":true,"score":0.95}"""u8.ToArray());
            var worker = new WorkflowWorker(runStore, "demo", time);
            int resumed = await worker.DeliverMessageAsync("kyc.verdict", null, verdict.RootElement, resumer, default).ConfigureAwait(false);
            log?.Invoke($"Live async onboarding run '{runId}': delivered kyc.verdict message, resumed {resumed} run(s) to completion.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"Live async onboarding run '{runId}' failed: {ex}");
        }
    }

    private static async ValueTask RunLiveAsync(IWorkflowStateStore runStore, WorkflowResumer resumer, TimeProvider time, string runId, string correlationId, string inputsJson, Action<string>? log, string workflowId = "onboard-customer-v1")
    {
        try
        {
            using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(inputsJson));
            using WorkflowRun run = WorkflowRun.CreateNew(runStore, runId, workflowId, inputs.RootElement, time, correlationId: correlationId, tags: TagSet.FromTags(["tenant-7"]));
            WorkflowRunResultKind result = await resumer(run, default).ConfigureAwait(false);
            log?.Invoke($"Live onboarding run '{runId}' executed against /svc: {result}.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"Live onboarding run '{runId}' failed: {ex}");
        }
    }

    // Prefixes the source's /svc base path onto each outgoing request (the host root is the client's base address).
    private sealed class SvcPrefixHandler(string prefix) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is { } uri)
            {
                request.RequestUri = new UriBuilder(uri) { Path = prefix + uri.AbsolutePath }.Uri;
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>Seeds the catalog with demo workflow versions and the run store with runs across every status.</summary>
    /// <param name="catalog">The catalog client.</param>
    /// <param name="runStore">The run state store.</param>
    /// <param name="specsDir">The directory holding the demo's Arazzo workflow + OpenAPI source documents.</param>
    /// <param name="timeProvider">The time provider (defaults to the system clock).</param>
    public static async ValueTask SeedAsync(SecuredWorkflowCatalog catalog, IWorkflowStateStore runStore, string specsDir, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(specsDir);
        TimeProvider time = timeProvider ?? TimeProvider.System;

        ReadOnlyMemory<byte> onboarding = Package(specsDir, "onboard-customer.arazzo.json", ("onboarding", "onboarding.openapi.json"));
        ReadOnlyMemory<byte> onboardingV2 = Package(specsDir, "onboard-customer.v2.arazzo.json", ("onboarding", "onboarding.openapi.json"));
        ReadOnlyMemory<byte> onboardingAsync = Package(specsDir, "onboard-customer.async.arazzo.json", ("onboarding", "onboarding.openapi.json"), ("notifications", "notifications.asyncapi.json"));
        ReadOnlyMemory<byte> onboardingRetry = Package(specsDir, "onboard-customer.retry.arazzo.json", ("onboarding", "onboarding.openapi.json"));
        ReadOnlyMemory<byte> reconcile = Package(specsDir, "nightly-reconcile.arazzo.json", ("ledger", "ledger.openapi.json"));

        // Catalog: onboarding (one active version) and nightly-reconcile (a v1 that we obsolete, plus an active v2).
        // Version 1's submitter identity establishes the workflow's §15 administrator set (design §15) — here the
        // arazzo-admins group, so any arazzo-admins member may approve access requests for these workflows (and the
        // grant's reach matches because the catalog stamps each version with its sys:workflow tag).
        SecurityTagSet adminFounder = SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "group", "arazzo-admins")]);
        await catalog.AddAsync(onboarding, OnboardingOwner, TagSet.FromTags(["prod", "kyc"]), adminFounder, default).ConfigureAwait(false);

        // onboard-customer v2: the resilient revision that routes a failed identity check to applicant notification
        // (verifyIdentity onFailure -> goto) instead of faulting. Catalogued as version 2 of the same workflow.
        await catalog.AddAsync(onboardingV2, OnboardingOwner, TagSet.FromTags(["prod", "kyc"]), adminFounder, default).ConfigureAwait(false);

        // onboard-customer-async: awaits an out-of-band KYC verdict (AsyncAPI kyc.verdict channel) — the run
        // suspends durably at the receive step until a verdict message is delivered, then resumes.
        await catalog.AddAsync(onboardingAsync, OnboardingOwner, TagSet.FromTags(["prod", "kyc"]), adminFounder, default).ConfigureAwait(false);

        // onboard-customer-retry: retries the identity check with a backoff on a transient provider outage — the
        // run suspends on a durable timer between attempts and resumes when the backoff elapses.
        await catalog.AddAsync(onboardingRetry, OnboardingOwner, TagSet.FromTags(["prod", "kyc"]), adminFounder, default).ConfigureAwait(false);

        await catalog.AddAsync(reconcile, ReconcileOwner, TagSet.FromTags(["prod", "billing"]), adminFounder, default).ConfigureAwait(false);
        await catalog.AddAsync(reconcile, ReconcileOwner, TagSet.FromTags(["prod", "billing", "beta"]), adminFounder, default).ConfigureAwait(false);
        (await catalog.UpdateAsync("nightly-reconcile", 1, owner: null, tags: null, status: CatalogStatus.Obsolete, AccessContext.System, default).ConfigureAwait(false)).Document?.Dispose();

        // Runs across statuses. Faulted runs sit on the steps whose outputs carry the rich shapes, so the
        // resume dialog's "Record outputs" editor shows the union / map / tuple controls — and validates them.
        await Faulted(runStore, time, "run-onb-7f3a9c21", "onboard-customer-v1", cursor: 1, "verifyIdentity", "KycProviderException: document image unreadable", ["tenant-7", "kyc"]).ConfigureAwait(false);
        await Faulted(runStore, time, "run-onb-b2c3d4e5", "onboard-customer-v1", cursor: 2, "provisionResources", "QuotaExceededException: region eu-west-1 database quota reached", ["tenant-7"]).ConfigureAwait(false);
        await SuspendedForMessage(runStore, time, "run-onb-9c0142ab", "onboard-customer-v1", cursor: 1, "kyc.results", "cust-55021", ["tenant-7"]).ConfigureAwait(false);
        await Completed(runStore, time, "run-onb-0a5512cd", "onboard-customer-v1", cursor: 4, ["tenant-7"]).ConfigureAwait(false);

        await Faulted(runStore, time, "run-rec-c9d8e7f6", "nightly-reconcile-v2", cursor: 3, "flagDiscrepancies", "TimeoutException: ledger service did not respond within 30s", ["prod", "billing"]).ConfigureAwait(false);
        await Running(runStore, time, "run-rec-33aa71f9", "nightly-reconcile-v2", cursor: 2, ["prod", "billing"]).ConfigureAwait(false);
        await SuspendedForTimer(runStore, time, "run-rec-1b88de40", "nightly-reconcile-v2", cursor: 4, TimeSpan.FromMinutes(45), ["prod", "billing"]).ConfigureAwait(false);
    }

    private static async ValueTask Faulted(IWorkflowStateStore store, TimeProvider time, string id, string workflowId, int cursor, string stepId, string error, string[] tags)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, workflowId, default, time, correlationId: id.Replace("run-", string.Empty, StringComparison.Ordinal), tags: TagSet.FromTags(tags));
        if (cursor > 0)
        {
            await run.CheckpointAsync(cursor, default).ConfigureAwait(false);
        }

        await run.FaultAsync(stepId, attempt: 1, error, default).ConfigureAwait(false);
    }

    private static async ValueTask Completed(IWorkflowStateStore store, TimeProvider time, string id, string workflowId, int cursor, string[] tags)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, workflowId, default, time, tags: TagSet.FromTags(tags));
        await run.CheckpointAsync(cursor, default).ConfigureAwait(false);
        await run.CompleteAsync(default, default).ConfigureAwait(false);
    }

    private static async ValueTask Running(IWorkflowStateStore store, TimeProvider time, string id, string workflowId, int cursor, string[] tags)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, workflowId, default, time, tags: TagSet.FromTags(tags));
        await run.CheckpointAsync(cursor, default).ConfigureAwait(false);
    }

    private static async ValueTask SuspendedForMessage(IWorkflowStateStore store, TimeProvider time, string id, string workflowId, int cursor, string channel, string correlationId, string[] tags)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, workflowId, default, time, tags: TagSet.FromTags(tags));
        await run.CheckpointAsync(cursor, default).ConfigureAwait(false);
        await run.SuspendForMessageAsync(cursor, channel, correlationId, default).ConfigureAwait(false);
    }

    private static async ValueTask SuspendedForTimer(IWorkflowStateStore store, TimeProvider time, string id, string workflowId, int cursor, TimeSpan delay, string[] tags)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, workflowId, default, time, tags: TagSet.FromTags(tags));
        await run.CheckpointAsync(cursor, default).ConfigureAwait(false);
        await run.SuspendForTimerAsync(cursor, delay, default).ConfigureAwait(false);
    }

    /// <summary>Builds a workflow package from on-disk spec files: an Arazzo workflow and its named sources.</summary>
    private static ReadOnlyMemory<byte> Package(string specsDir, string workflowFile, params (string Name, string File)[] sources)
    {
        var sourceList = new List<KeyValuePair<string, byte[]>>(sources.Length);
        foreach ((string name, string file) in sources)
        {
            sourceList.Add(new KeyValuePair<string, byte[]>(name, File.ReadAllBytes(Path.Combine(specsDir, file))));
        }

        return WorkflowPackage.Pack(File.ReadAllBytes(Path.Combine(specsDir, workflowFile)), sourceList);
    }
}
