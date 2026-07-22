// <copyright file="KycVerdictResumeHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Samples.Notifications;
using Microsoft.Extensions.Logging;
using NModels = Corvus.Text.Json.Arazzo.Samples.Notifications.Models;

namespace Corvus.Text.Json.Arazzo.Runner.Demo;

/// <summary>
/// The consumer side of the KYC verdict exchange in the runner. It SUBSCRIBES to the <c>kyc.verdict</c> channel and,
/// for each verdict, delivers the message to the workflow run suspended awaiting it — matched by the account-id
/// correlation the run registered when it sent its review request — resuming that run (and only that run) over the
/// shared durable store. This is what makes the async KYC verdict flow through the real broker rather than an
/// in-process synthetic delivery.
/// </summary>
public sealed class KycVerdictResumeHandler : IReceiveKycVerdictHandler
{
    private readonly WorkflowWorker worker;
    private readonly WorkflowResumer resumer;
    private readonly string? runnerEnvironment;
    private readonly ILogger<KycVerdictResumeHandler> logger;

    /// <summary>Initializes a new instance of the <see cref="KycVerdictResumeHandler"/> class.</summary>
    /// <param name="worker">The worker that leases + resumes suspended runs over the shared store.</param>
    /// <param name="resumer">The resumer that re-enters a run's generated executor.</param>
    /// <param name="runnerEnvironment">The single environment this runner serves (§5.5), so a verdict resumes only a run
    /// pinned to it and never one in another environment that happens to await the same channel. A runner always supplies
    /// its environment; <see langword="null"/> is the env-agnostic form (an in-process host).</param>
    /// <param name="logger">Logs each verdict receipt and how many suspended runs it resumed, so the async exchange is visible in the runner's logs.</param>
    public KycVerdictResumeHandler(WorkflowWorker worker, WorkflowResumer resumer, string? runnerEnvironment, ILogger<KycVerdictResumeHandler> logger)
    {
        this.worker = worker ?? throw new ArgumentNullException(nameof(worker));
        this.resumer = resumer ?? throw new ArgumentNullException(nameof(resumer));
        this.runnerEnvironment = runnerEnvironment;
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async ValueTask HandleKycVerdictAsync(NModels.KycVerdictPayload payload, CancellationToken cancellationToken = default)
    {
        var element = (JsonElement)payload;
        string? accountId = element.TryGetProperty("accountId"u8, out JsonElement a) && a.ValueKind == JsonValueKind.String
            ? a.GetString()
            : null;

        // Deliver on the channel matched by the account-id correlation: only the run that suspended awaiting this
        // account's verdict resumes; any other suspended async runs stay put.
        int resumed = await this.worker.DeliverMessageAsync("kyc.verdict", accountId, element, this.resumer, this.runnerEnvironment, cancellationToken).ConfigureAwait(false);

        // Make the async exchange visible: without this the runner resumes the run silently and the operator sees "nothing happened".
        this.logger.LogInformation(
            "KYC verdict received for account {AccountId}; resumed {ResumedCount} suspended run(s).",
            accountId ?? "(none)",
            resumed);
    }
}
