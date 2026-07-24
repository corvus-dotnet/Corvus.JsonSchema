// <copyright file="DraftRunHost.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.Runner;

/// <summary>
/// The out-of-process §18 draft-run host. In the multi-process topology the control plane only MARKS $draft debug
/// runs claimable; THIS runner claims and executes them — the true two-plane split (design §18). It composes an
/// <see cref="InProcessDraftRunner"/> pinned to the runner's environment over the shared durability stores.
/// </summary>
public static class DraftRunHost
{
    /// <summary>Builds a transport binder over a caller-supplied source-name → client map (no credentials) — the
    /// un-credentialed sibling of <c>SourceCredentialTransports.CreateBinder</c>, used on the standalone (no-Vault)
    /// path so the same client map (each source's real service) routes both the credentialed and un-credentialed paths.</summary>
    /// <param name="clients">The source-name → client map (must contain every source a run declares).</param>
    /// <param name="messageTransport">The message transport every channel source binds to, or <see langword="null"/>
    /// when the host binds no message workflows (the un-credentialed path serves one broker; the credentialed path
    /// binds per channel source through the channel-transport cache, ADR 0051).</param>
    /// <returns>The transport binder.</returns>
    public static WorkflowTransportBinder CreateBinder(IReadOnlyDictionary<string, HttpClient> clients, IMessageTransport? messageTransport = null)
    {
        ArgumentNullException.ThrowIfNull(clients);
        return (descriptor, runTags) => new WorkflowTransports(
            descriptor.Sources.ToDictionary(
                source => source,
                source => (IApiTransport)new HttpClientApiTransportFactory(clients[source]).CreateTransport(),
                StringComparer.Ordinal),
            descriptor.MessageSources.Count > 0 && messageTransport is { } transport
                ? descriptor.MessageSources.ToDictionary(source => source.Name, _ => transport, StringComparer.Ordinal)
                : WorkflowTransports.NoMessageTransports);
    }
}

/// <summary>Pumps the out-of-process <see cref="InProcessDraftRunner"/>: it claims the $draft debug runs the control
/// plane marked and advances them (execute / step / resume), recording the trace — the execution the control plane
/// deliberately never performs itself.</summary>
public sealed class DraftRunPumpService(InProcessDraftRunner runner, ILogger<DraftRunPumpService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await runner.RunPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Draft-run pump failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
