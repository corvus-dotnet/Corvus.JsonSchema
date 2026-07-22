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
/// registered when it sent its approval-required notification — resuming that run, and only that run, over the shared
/// durable store. This is what advances a governed approval once an administrator's decision is published.
/// </summary>
public sealed class AccessDecisionResumeHandler : IReceiveAccessDecisionHandler
{
    /// <summary>The channel the decision is delivered on — the same channel the suspended run awaits.</summary>
    private const string DecisionChannel = "access.decision";

    private readonly WorkflowWorker worker;
    private readonly WorkflowResumer resumer;
    private readonly string? runnerEnvironment;
    private readonly ILogger<AccessDecisionResumeHandler> logger;

    /// <summary>Initializes a new instance of the <see cref="AccessDecisionResumeHandler"/> class.</summary>
    /// <param name="worker">The worker that leases and resumes suspended runs over the shared durable store.</param>
    /// <param name="resumer">The resumer that re-enters a run's generated executor.</param>
    /// <param name="runnerEnvironment">The single environment this runner serves (§5.5), so a decision resumes only an
    /// approval run pinned to it and never one in another environment that happens to await the same channel. A runner
    /// always supplies its environment; <see langword="null"/> is the env-agnostic form (an in-process host).</param>
    /// <param name="logger">Logs each decision receipt and how many suspended runs it resumed.</param>
    public AccessDecisionResumeHandler(WorkflowWorker worker, WorkflowResumer resumer, string? runnerEnvironment, ILogger<AccessDecisionResumeHandler> logger)
    {
        this.worker = worker ?? throw new ArgumentNullException(nameof(worker));
        this.resumer = resumer ?? throw new ArgumentNullException(nameof(resumer));
        this.runnerEnvironment = runnerEnvironment;
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

        int resumed = await this.worker.DeliverMessageAsync(
            DecisionChannel, requestId, (JsonElement)payload, this.resumer, this.runnerEnvironment, cancellationToken).ConfigureAwait(false);

        // Make the exchange visible: without this the runner resumes the run silently and the operator sees nothing.
        this.logger.LogInformation(
            "Access decision received for request {RequestId}; resumed {ResumedCount} suspended run(s).",
            requestId ?? "(none)",
            resumed);
    }
}