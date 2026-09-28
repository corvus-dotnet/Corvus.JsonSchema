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
    private readonly IWorkflowMessageDelivery delivery;
    private readonly ILogger<KycVerdictResumeHandler> logger;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="KycVerdictResumeHandler"/> class.</summary>
    /// <param name="delivery">Delivers the verdict to the runs awaiting it. Over the runner API the candidate set is
    /// intersected server-side with the environments this runner's machine principal is bound to, so the environment
    /// filter this handler used to apply is enforced rather than cooperative.</param>
    /// <param name="logger">Logs each verdict receipt and how many suspended runs it resumed, so the async exchange is visible in the runner's logs.</param>
    /// <param name="timeProvider">The time source for the pauses while a verdict waits for its run to suspend; defaults
    /// to <see cref="TimeProvider.System"/>.</param>
    public KycVerdictResumeHandler(IWorkflowMessageDelivery delivery, ILogger<KycVerdictResumeHandler> logger, TimeProvider? timeProvider = null)
    {
        this.delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask HandleKycVerdictAsync(NModels.KycVerdictPayload payload, CancellationToken cancellationToken = default)
    {
        var element = (JsonElement)payload;
        string? accountId = element.TryGetProperty("accountId"u8, out JsonElement a) && a.ValueKind == JsonValueKind.String
            ? a.GetString()
            : null;

        // Deliver on the channel matched by the account-id correlation: only the run that suspended awaiting this
        // account's verdict resumes; any other suspended async runs stay put. The run sends its review request and then
        // suspends, so a prompt verdict can arrive before the run awaits it; the delivery tries again for a bounded window
        // rather than dropping it.
        int resumed = await this.delivery.DeliverToAwaitingRunAsync(
            "kyc.verdict", accountId, element, this.timeProvider, cancellationToken).ConfigureAwait(false);

        // Make the async exchange visible: without this the runner resumes the run silently and the operator sees "nothing happened".
        this.logger.LogInformation(
            "KYC verdict received for account {AccountId}; resumed {ResumedCount} suspended run(s).",
            accountId ?? "(none)",
            resumed);
    }
}