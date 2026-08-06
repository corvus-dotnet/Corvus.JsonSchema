// <copyright file="AccessDecisionResumeHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using SwModels = Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;

/// <summary>
/// The receive side of the access-decision exchange in the control-plane system runner (design §16.5.1). It subscribes
/// (through <see cref="ReceiveAccessDecisionConsumer"/>) to the <c>access.decision</c> channel and, for each decision,
/// delivers the message to the approval run suspended awaiting it — matched by the request-id correlation the run
/// registered when it sent its approval-required notification — resuming that run, and only that run. This is what
/// advances a governed approval once an administrator's decision is published.
///
/// How the delivery reaches durable state is the host's choice, not this handler's: a system runner reaches it through
/// the runner API and holds no store credential (ADR 0065), while an in-process host reaches the store directly.
/// </summary>
public sealed class AccessDecisionResumeHandler : IReceiveAccessDecisionHandler
{
    /// <summary>The channel the decision is delivered on — the same channel the suspended run awaits.</summary>
    private const string DecisionChannel = "access.decision";

    private readonly IWorkflowMessageDelivery delivery;
    private readonly ILogger<AccessDecisionResumeHandler> logger;

    /// <summary>Initializes a new instance of the <see cref="AccessDecisionResumeHandler"/> class.</summary>
    /// <param name="delivery">Delivers the decision to the runs awaiting it. The environment scoping (or, over the
    /// runner API, the server-side binding intersection that replaces it) is bound into the delivery rather than
    /// decided here.</param>
    /// <param name="logger">Logs each decision receipt and how many suspended runs it resumed.</param>
    public AccessDecisionResumeHandler(IWorkflowMessageDelivery delivery, ILogger<AccessDecisionResumeHandler> logger)
    {
        this.delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async ValueTask HandleAccessDecisionAsync(SwModels.AccessDecisionPayload payload, CancellationToken cancellationToken = default)
    {
        // The run registered its correlation from the requestId it sent, so match on the same key: only the run awaiting
        // THIS request's decision resumes; any other suspended approval runs stay put.
        //
        // Realising the requestId to a System.String here is the correct realise-at-leaf shape, not an avoidable
        // allocation: the wait-index correlation lookup (IWorkflowWaitIndex.QueryAwaitingAsync) bottoms out at a string
        // at every durable backend — a TEXT SQL parameter (Sqlite/Postgres/SqlServer/MySql), a RedisValue, a BsonString
        // (Mongo), and Cosmos/Table string compares. A UTF-8 correlation would have to be re-stringified at each of those
        // leaves, adding allocations rather than removing them. So decode once, at this boundary, and pass the string down.
        SwModels.JsonString requestIdValue = payload.RequestId;
        string? requestId = requestIdValue.IsNotUndefined() ? (string)requestIdValue : null;

        int resumed = await this.delivery.DeliverAsync(
            DecisionChannel, requestId, (JsonElement)payload, cancellationToken).ConfigureAwait(false);

        // Make the exchange visible: without this the runner resumes the run silently and the operator sees nothing.
        // Resuming zero runs is an ANOMALY, not routine: a decision was published for a request that has no approval run
        // suspended awaiting it, so the decision cannot be enacted and the request will stay pending forever. That happens
        // when a pending request was written to the store WITHOUT starting its approval run (design §16.5.1 requires
        // submission to go through the approval service). Surface it at warning so the operator sees the request will not
        // settle, rather than mistaking silence for success.
        if (resumed == 0)
        {
            this.logger.LogWarning(
                "Access decision received for request {RequestId} but no suspended approval run was awaiting it (resumed 0); the request cannot be enacted and will stay pending. Was it created without starting an approval run?",
                requestId ?? "(none)");
        }
        else
        {
            this.logger.LogInformation(
                "Access decision received for request {RequestId}; resumed {ResumedCount} suspended run(s).",
                requestId ?? "(none)",
                resumed);
        }
    }
}