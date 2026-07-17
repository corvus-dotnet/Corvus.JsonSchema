// <copyright file="DemoData.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.AsyncApi;
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

    /// <summary>The reserved issuer id (<c>sys:iss</c>, §16.5.5) stamped on every Keycloak identity in this deployment, so
    /// its identities are disjoint from any other provider's AND set-equal the grantee-directory adapter's resolved grants
    /// (the adapter always stamps <c>sys:iss</c>). The runtime internal-tag resolver (Program.cs), the seeded admin
    /// identities (here + ExampleSeed), the observed-store seed, and the directory options all read THIS constant — one
    /// source of truth, so an administrator match holds across the live caller, the seed, and a directory pick.</summary>
    public const string KeycloakIssuer = "arazzo-keycloak";

    /// <summary>The reserved <c>sys:iss</c> issuer tag for this deployment (<see cref="KeycloakIssuer"/>).</summary>
    public static SecurityTag IssuerTag => new(SecurityShell.DefaultInternalPrefix + "iss", KeycloakIssuer);

    /// <summary>A Keycloak group identity aligned with the runtime stamper: <c>{sys:group=&lt;group&gt;, sys:iss}</c> — the
    /// shape the live <c>internalTagResolver</c> resolves a single-group caller to, so a seeded or directory-resolved grant
    /// on it set-equals that caller (<see cref="WorkflowIdentity.SameAdministrator"/>).</summary>
    public static SecurityTagSet GroupIdentity(string group)
        => SecurityTagSet.FromTags([new SecurityTag(SecurityShell.DefaultInternalPrefix + "group", group), IssuerTag]);

    /// <summary>Builds the live <see cref="WorkflowResumer"/> that re-enters a run's compiled executor (§5/§8): it
    /// resolves the run's catalogued executor through the loader and runs it against transports rooted at the sample's
    /// real services (onboarding, ledger, kyc) and its NATS message bus. <paramref name="baseUrlProvider"/> yields the
    /// host's base URL once it has started listening (the resumer is built before the server binds, but invoked only after).</summary>
    /// <param name="catalog">The catalog store the baked executor is loaded from.</param>
    /// <param name="baseUrlProvider">Yields this host's base URL (resolved after it starts listening).</param>
    /// <param name="onboardingBaseUrl">The onboarding service's base URL (its own external host).</param>
    /// <returns>The resumer delegate.</returns>
    public static WorkflowResumer CreateLiveResumer(IWorkflowCatalogStore catalog, Func<string> baseUrlProvider, string onboardingBaseUrl, string ledgerBaseUrl, string kycBaseUrl, IMessageTransport messageTransport)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(baseUrlProvider);

        var resumer = new HostedWorkflowResumer(catalog, new WorkflowExecutorLoader(), CreateLiveBinder(baseUrlProvider, onboardingBaseUrl, ledgerBaseUrl, kycBaseUrl, messageTransport));
        return resumer.AsResumer();
    }

    /// <summary>Builds the live-execution transport binder shared by the catalog resumer (<see cref="CreateLiveResumer"/>)
    /// and the §18 in-process draft runner. The <c>onboarding</c>, <c>ledger</c>, and <c>kyc</c> sources are real
    /// external services (their own hosts + databases), so their clients are rooted directly at those services. The
    /// demo has no control-plane <c>/svc</c> mock left (notifications is an AsyncAPI message source, not an HTTP one),
    /// so that branch is a defensive fallback. In production every source is a real endpoint and this is
    /// <c>SourceCredentialTransports.CreateBinder</c> (credentials resolved as the runner's identity via Vault).</summary>
    /// <param name="baseUrlProvider">Yields this host's base URL (the defensive /svc fallback root).</param>
    /// <param name="onboardingBaseUrl">The onboarding service's base URL (its own external host).</param>
    /// <param name="ledgerBaseUrl">The ledger service's base URL (its own external host).</param>
    /// <param name="kycBaseUrl">The KYC service's base URL (its own external host).</param>
    /// <param name="messageTransport">The application's message bus (NATS JetStream) — an AsyncAPI send step (e.g. the
    /// async workflow's requestKycReview) publishes through it; the durable verdict receive suspends and is resumed by a
    /// separate consumer, so this is used only for sends here.</param>
    /// <returns>The transport binder.</returns>
    public static WorkflowTransportBinder CreateLiveBinder(Func<string> baseUrlProvider, string onboardingBaseUrl, string ledgerBaseUrl, string kycBaseUrl, IMessageTransport messageTransport)
    {
        ArgumentNullException.ThrowIfNull(baseUrlProvider);
        ArgumentException.ThrowIfNullOrEmpty(onboardingBaseUrl);
        ArgumentException.ThrowIfNullOrEmpty(ledgerBaseUrl);
        ArgumentException.ThrowIfNullOrEmpty(kycBaseUrl);
        ArgumentNullException.ThrowIfNull(messageTransport);

        // The onboarding, ledger, and kyc clients are rooted directly at their services (their absolute operation
        // paths hit the service). Any other source would be a control-plane /svc mock — the generated client emits
        // absolute paths, so rooting at the control-plane host + prefixing /svc/<source> routes them there — but the
        // demo has none left, so that is a defensive fallback.
        var onboardingUri = new Uri(onboardingBaseUrl);
        var ledgerUri = new Uri(ledgerBaseUrl);
        var kycUri = new Uri(kycBaseUrl);
        var clients = new ConcurrentDictionary<string, HttpClient>(StringComparer.Ordinal);
        HttpClient ClientFor(string source) => clients.GetOrAdd(source, s => s switch
        {
            "onboarding" => new HttpClient { BaseAddress = onboardingUri },
            "ledger" => new HttpClient { BaseAddress = ledgerUri },
            "kyc" => new HttpClient { BaseAddress = kycUri },
            _ => new HttpClient(new SvcPrefixHandler($"/svc/{s}") { InnerHandler = new HttpClientHandler() }) { BaseAddress = new Uri(baseUrlProvider()) },
        });

        // A workflow with an AsyncAPI send step (e.g. the async workflow's requestKycReview) publishes through the
        // supplied message transport (the real NATS JetStream bus); the durable verdict receive suspends rather than
        // blocking on it, and a separate consumer resumes the run. Shared across runs.
        return (descriptor, runTags) => new WorkflowTransports(
            descriptor.Sources.ToDictionary(
                source => source,
                source => (IApiTransport)new HttpClientApiTransportFactory(ClientFor(source)).CreateTransport(),
                StringComparer.Ordinal),
            descriptor.NeedsMessageTransport ? messageTransport : null);
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
        await RunLiveSuspendAwaitingKycAsync(runStore, resumer, time, "run-onb-live04", "live04", log).ConfigureAwait(false);

        // A resilient onboarding: the identity provider returns a transient incomplete result on the first check,
        // so the step retries with a backoff — the run suspends on a durable TIMER between attempts and resumes
        // when the backoff elapses, succeeding on the retry. Live timer suspend + resume.
        await RunLiveTimerResumeAsync(runStore, resumer, time, "run-onb-live05", "live05", log).ConfigureAwait(false);

        // A real nightly reconciliation executed against the live ledger service — loads the account book, matches
        // entries, flags the seeded discrepancies, posts corrections, and publishes the report, completing end to end.
        await RunLiveReconcileAsync(runStore, resumer, time, "run-rec-live01", "reclive01", log).ConfigureAwait(false);
    }

    private static async ValueTask RunLiveTimerResumeAsync(IWorkflowStateStore runStore, WorkflowResumer resumer, TimeProvider time, string runId, string correlationId, Action<string>? log)
    {
        try
        {
            // First leg: the identity check fails (transient incomplete result), so the step schedules a retry with
            // a backoff and the run suspends on a durable timer (returns Suspended).
            using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"email":"katherine@example.com","fullName":"Katherine Transient","plan":"pro"}"""u8.ToArray());
            using WorkflowRun run = WorkflowRun.CreateNew(runStore, runId, "onboard-customer-retry-v1", inputs.RootElement, "development", time, correlationId: correlationId, tags: TagSet.FromTags(["tenant-7"]));
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

    private static async ValueTask RunLiveReconcileAsync(IWorkflowStateStore runStore, WorkflowResumer resumer, TimeProvider time, string runId, string correlationId, Action<string>? log)
    {
        try
        {
            // A real nightly reconciliation over the live ledger service (nightly-reconcile v2). Executes every step
            // against the ledger backend and completes with the computed counts + discrepancy report.
            string date = time.GetUtcNow().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes($$"""{"date":"{{date}}"}"""));
            using WorkflowRun run = WorkflowRun.CreateNew(runStore, runId, "nightly-reconcile-v2", inputs.RootElement, "development", time, correlationId: correlationId, tags: TagSet.FromTags(["prod", "billing"]));
            WorkflowRunResultKind result = await resumer(run, default).ConfigureAwait(false);
            log?.Invoke($"Live nightly-reconcile run '{runId}' executed against the ledger service: {result}.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"Live nightly-reconcile run '{runId}' failed: {ex}");
        }
    }

    private static async ValueTask RunLiveSuspendAwaitingKycAsync(IWorkflowStateStore runStore, WorkflowResumer resumer, TimeProvider time, string runId, string correlationId, Action<string>? log)
    {
        try
        {
            // Execute the run: create the account, PUBLISH a KYC review request to the real bus (kyc.requests — the KYC
            // service records it as a pending review), then SUSPEND at the awaitKycVerdict receive step. The run stays
            // suspended awaiting an out-of-band verdict; there is no synthetic delivery — an operator resumes it for real
            // by submitting a verdict (POST /accounts/{id}/kyc-verdict on the KYC service), which publishes to kyc.verdict
            // and the runner's consumer resumes it. This is a genuine pending-manual-recovery case in the demo's data.
            using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"email":"grace@example.com","fullName":"Grace Hopper","plan":"enterprise"}"""u8.ToArray());
            using WorkflowRun run = WorkflowRun.CreateNew(runStore, runId, "onboard-customer-async-v1", inputs.RootElement, "development", time, correlationId: correlationId, tags: TagSet.FromTags(["tenant-7"]));
            WorkflowRunResultKind first = await resumer(run, default).ConfigureAwait(false);
            log?.Invoke($"Live async onboarding run '{runId}': {first} (review requested on kyc.requests; awaiting an out-of-band verdict).");
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
            using WorkflowRun run = WorkflowRun.CreateNew(runStore, runId, workflowId, inputs.RootElement, "development", time, correlationId: correlationId, tags: TagSet.FromTags(["tenant-7"]));
            WorkflowRunResultKind result = await resumer(run, default).ConfigureAwait(false);
            log?.Invoke($"Live onboarding run '{runId}' executed against its real sources: {result}.");
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

    /// <summary>Seeds the catalog with the demo workflow versions. The runs themselves are executed live (real, not
    /// hand-seeded) via <see cref="RunLiveOnboardingAsync"/>, so every run in the list is genuinely resumable.</summary>
    /// <param name="catalog">The catalog client.</param>
    /// <param name="specsDir">The directory holding the demo's Arazzo workflow + OpenAPI source documents.</param>
    public static async ValueTask SeedAsync(SecuredWorkflowCatalog catalog, string specsDir)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(specsDir);

        ReadOnlyMemory<byte> onboarding = Package(specsDir, "onboard-customer.arazzo.json", ("onboarding", "onboarding.openapi.json"), ("kyc", "kyc.openapi.json"));
        ReadOnlyMemory<byte> onboardingV2 = Package(specsDir, "onboard-customer.v2.arazzo.json", ("onboarding", "onboarding.openapi.json"), ("kyc", "kyc.openapi.json"));
        ReadOnlyMemory<byte> onboardingAsync = Package(specsDir, "onboard-customer.async.arazzo.json", ("onboarding", "onboarding.openapi.json"), ("notifications", "notifications.asyncapi.json"));
        ReadOnlyMemory<byte> onboardingRetry = Package(specsDir, "onboard-customer.retry.arazzo.json", ("onboarding", "onboarding.openapi.json"), ("kyc", "kyc.openapi.json"));
        ReadOnlyMemory<byte> reconcile = Package(specsDir, "nightly-reconcile.arazzo.json", ("ledger", "ledger.openapi.json"));

        // Catalog: onboarding (one active version) and nightly-reconcile (a v1 that we obsolete, plus an active v2).
        // Version 1's submitter identity establishes the workflow's §15 administrator set (design §15) — here the
        // arazzo-admins group, so any arazzo-admins member may approve access requests for these workflows (and the
        // grant's reach matches because the catalog stamps each version with its sys:workflow tag).
        SecurityTagSet adminFounder = GroupIdentity("arazzo-admins");
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
        (await catalog.UpdateAsync("nightly-reconcile", 1, owner: null, tags: null, status: CatalogStatus.Obsolete, securityTags: null, internalTagPrefix: null, AccessContext.System, default).ConfigureAwait(false)).Document?.Dispose();

        // The onboarding flows process KYC identity data, so their authors classify step outputs sensitive (§14): a run's
        // step journal is redacted for callers below the stronger grant — write reach on the run — so a reach-scoped
        // observer sees each step withheld, while an operator/administrator reads it in full.
        foreach ((string BaseWorkflowId, int VersionNumber) v in new[] { ("onboard-customer", 1), ("onboard-customer", 2), ("onboard-customer-async", 1), ("onboard-customer-retry", 1) })
        {
            (await catalog.UpdateAsync(v.BaseWorkflowId, v.VersionNumber, owner: null, tags: null, status: null, securityTags: null, internalTagPrefix: null, AccessContext.System, default, OutputsSensitivity.Sensitive).ConfigureAwait(false)).Document?.Dispose();
        }
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
