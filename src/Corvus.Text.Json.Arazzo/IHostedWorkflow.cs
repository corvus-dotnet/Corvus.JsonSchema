// <copyright file="IHostedWorkflow.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The non-generic contract an execution host drives a loaded workflow executor through. The generated
/// <c>ExecuteAsync</c> is <see langword="static"/> and generic over the workflow's input/output types; the
/// code generator also emits an adapter implementing this interface so a host can run the workflow without
/// referencing those generated types. The interface lives in the runtime, so the host and the dynamically
/// loaded executor assembly share one contract.
/// </summary>
/// <remarks>
/// A host obtains an instance by loading the executor assembly (<c>metadata/executor.dll</c>) and activating
/// the manifest's <c>entryType</c>. <see cref="RunAsync"/> wraps the durable execution path, so it serves both
/// a fresh trigger (a newly created run) and a resumed checkpoint — it is the <c>WorkflowResumer</c> the
/// durable worker already expects.
/// </remarks>
public interface IHostedWorkflow
{
    /// <summary>Gets the host-facing description of this workflow (id, transport needs, source bindings).</summary>
    WorkflowDescriptor Descriptor { get; }

    /// <summary>
    /// Starts or resumes a durable run of the workflow, returning the tri-state outcome.
    /// </summary>
    /// <param name="apiTransports">The API transports the workflow's OpenAPI operation steps call through, keyed by source-description name (see <see cref="WorkflowDescriptor.Sources"/>). A single-source workflow's adapter reads its sole source from this map; a multi-source workflow selects per source.</param>
    /// <param name="messageTransports">The message transports the workflow's AsyncAPI channel steps send/receive on, keyed by channel-source name (see <see cref="WorkflowDescriptor.MessageSources"/>, ADR 0051) — each bound from that environment's channel credential. Empty when <see cref="WorkflowDescriptor.NeedsMessageTransport"/> is <see langword="false"/>.</param>
    /// <param name="workspace">The JSON workspace the run builds its values in.</param>
    /// <param name="inputs">The run's inputs as a <see cref="JsonElement"/> (the host reads these from the run record); the adapter parses them into the workflow's generated inputs type.</param>
    /// <param name="run">The durable run to start (a freshly created run) or resume (a restored checkpoint).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run outcome: completed, faulted, or suspended.</returns>
    ValueTask<WorkflowRunResultKind> RunAsync(
        IReadOnlyDictionary<string, IApiTransport> apiTransports,
        IReadOnlyDictionary<string, IMessageTransport> messageTransports,
        JsonWorkspace workspace,
        JsonElement inputs,
        IWorkflowRun run,
        CancellationToken cancellationToken);
}