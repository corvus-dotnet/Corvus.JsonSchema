// <copyright file="DemoData.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;

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

    /// <summary>A resumer that drives a resumed run to completion (stands in for re-entering a generated executor).</summary>
    public static async ValueTask<WorkflowRunResultKind> CompleteResumer(WorkflowRun run, CancellationToken cancellationToken)
    {
        await run.CompleteAsync(default, cancellationToken).ConfigureAwait(false);
        return WorkflowRunResultKind.Completed;
    }

    /// <summary>Seeds the catalog with demo workflow versions and the run store with runs across every status.</summary>
    /// <param name="catalog">The catalog client.</param>
    /// <param name="runStore">The run state store.</param>
    /// <param name="specsDir">The directory holding the demo's Arazzo workflow + OpenAPI source documents.</param>
    /// <param name="timeProvider">The time provider (defaults to the system clock).</param>
    public static async ValueTask SeedAsync(WorkflowCatalogClient catalog, IWorkflowStateStore runStore, string specsDir, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(specsDir);
        TimeProvider time = timeProvider ?? TimeProvider.System;

        ReadOnlyMemory<byte> onboarding = Package(specsDir, "onboard-customer.arazzo.json", ("onboarding", "onboarding.openapi.json"));
        ReadOnlyMemory<byte> reconcile = Package(specsDir, "nightly-reconcile.arazzo.json", ("ledger", "ledger.openapi.json"));

        // Catalog: onboarding (one active version) and nightly-reconcile (a v1 that we obsolete, plus an active v2).
        await catalog.AddAsync(onboarding, OnboardingOwner, ["prod", "kyc"], default).ConfigureAwait(false);
        await catalog.AddAsync(reconcile, ReconcileOwner, ["prod", "billing"], default).ConfigureAwait(false);
        await catalog.AddAsync(reconcile, ReconcileOwner, ["prod", "billing", "beta"], default).ConfigureAwait(false);
        await catalog.UpdateAsync("nightly-reconcile", 1, owner: null, tags: null, status: CatalogStatus.Obsolete, default).ConfigureAwait(false);

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
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, workflowId, default, time, correlationId: id.Replace("run-", string.Empty, StringComparison.Ordinal), tags: tags);
        if (cursor > 0)
        {
            await run.CheckpointAsync(cursor, default).ConfigureAwait(false);
        }

        await run.FaultAsync(stepId, attempt: 1, error, default).ConfigureAwait(false);
    }

    private static async ValueTask Completed(IWorkflowStateStore store, TimeProvider time, string id, string workflowId, int cursor, string[] tags)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, workflowId, default, time, tags: tags);
        await run.CheckpointAsync(cursor, default).ConfigureAwait(false);
        await run.CompleteAsync(default, default).ConfigureAwait(false);
    }

    private static async ValueTask Running(IWorkflowStateStore store, TimeProvider time, string id, string workflowId, int cursor, string[] tags)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, workflowId, default, time, tags: tags);
        await run.CheckpointAsync(cursor, default).ConfigureAwait(false);
    }

    private static async ValueTask SuspendedForMessage(IWorkflowStateStore store, TimeProvider time, string id, string workflowId, int cursor, string channel, string correlationId, string[] tags)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, workflowId, default, time, tags: tags);
        await run.CheckpointAsync(cursor, default).ConfigureAwait(false);
        await run.SuspendForMessageAsync(cursor, channel, correlationId, default).ConfigureAwait(false);
    }

    private static async ValueTask SuspendedForTimer(IWorkflowStateStore store, TimeProvider time, string id, string workflowId, int cursor, TimeSpan delay, string[] tags)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, workflowId, default, time, tags: tags);
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
