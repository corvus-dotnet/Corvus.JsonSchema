// <copyright file="DraftRunHost.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.Runner.Demo;

/// <summary>
/// The out-of-process §18 draft-run host. In the multi-process topology the control plane only MARKS $draft debug
/// runs claimable; THIS runner claims and executes them — the true two-plane split (design §18). It composes an
/// <see cref="InProcessDraftRunner"/> pinned to the runner's environment over the shared durability stores.
/// </summary>
internal static class DraftRunHost
{
    /// <summary>Builds the transport binder the runner executes debug runs through — each declared source's generated
    /// client rooted at the CONTROL PLANE host with the <c>/svc/&lt;source&gt;</c> prefix (the demo's "real endpoints"
    /// are the control plane's own /svc backends). The production binder is the Vault-credentialed
    /// <c>SourceCredentialTransports.CreateBinder</c>, which resolves secrets as the runner's own identity (§13.5).</summary>
    /// <param name="sourcesBaseUrl">The base URL the source clients are rooted at (the control plane host).</param>
    /// <returns>The transport binder.</returns>
    public static WorkflowTransportBinder CreateSvcBinder(string sourcesBaseUrl)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcesBaseUrl);
        var clients = new ConcurrentDictionary<string, HttpClient>(StringComparer.Ordinal);
        HttpClient ClientFor(string source) => clients.GetOrAdd(source, s => new HttpClient(new SvcPrefixHandler($"/svc/{s}") { InnerHandler = new HttpClientHandler() })
        {
            BaseAddress = new Uri(sourcesBaseUrl),
        });

        return (descriptor, runTags) => new WorkflowTransports(
            descriptor.Sources.ToDictionary(
                source => source,
                source => (IApiTransport)new HttpClientApiTransportFactory(ClientFor(source)).CreateTransport(),
                StringComparer.Ordinal),
            null);
    }

    /// <summary>Builds one <see cref="HttpClient"/> per named source, each rooted at the control plane host with the
    /// source's <c>/svc/&lt;source&gt;</c> prefix — the endpoint set the §13.5 credential-aware binder applies each
    /// source's Vault-resolved secret to (the demo routes at the control plane's own /svc backends; the credential is
    /// real, the endpoint is the demo stand-in for a real one).</summary>
    /// <param name="sourcesBaseUrl">The control plane host the source clients are rooted at.</param>
    /// <param name="sources">The source names to build clients for.</param>
    /// <returns>A source-name → client map for <c>SourceCredentialTransports.CreateBinder</c>.</returns>
    public static Dictionary<string, HttpClient> CreateSvcClients(string sourcesBaseUrl, params string[] sources)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcesBaseUrl);
        return sources.ToDictionary(
            source => source,
            source => new HttpClient(new SvcPrefixHandler($"/svc/{source}") { InnerHandler = new HttpClientHandler() }) { BaseAddress = new Uri(sourcesBaseUrl) },
            StringComparer.Ordinal);
    }

    // Prefixes the source's /svc base path onto each outgoing request (the control plane host root is the client's base address).
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
}

/// <summary>Pumps the out-of-process <see cref="InProcessDraftRunner"/>: it claims the $draft debug runs the control
/// plane marked and advances them (execute / step / resume), recording the trace — the execution the control plane
/// deliberately never performs itself.</summary>
internal sealed class DraftRunPumpService(InProcessDraftRunner runner, ILogger<DraftRunPumpService> logger) : BackgroundService
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
