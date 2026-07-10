// <copyright file="KycVerdictResumeHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Samples.Notifications;
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

    /// <summary>Initializes a new instance of the <see cref="KycVerdictResumeHandler"/> class.</summary>
    /// <param name="worker">The worker that leases + resumes suspended runs over the shared store.</param>
    /// <param name="resumer">The resumer that re-enters a run's generated executor.</param>
    public KycVerdictResumeHandler(WorkflowWorker worker, WorkflowResumer resumer)
    {
        this.worker = worker ?? throw new ArgumentNullException(nameof(worker));
        this.resumer = resumer ?? throw new ArgumentNullException(nameof(resumer));
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
        await this.worker.DeliverMessageAsync("kyc.verdict", accountId, element, this.resumer, cancellationToken).ConfigureAwait(false);
    }
}
