// <copyright file="SourceCredentialTransports.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// Composes the §13 source-credential cache with the HTTP transport so a runner's per-source transports authenticate
/// from operator-managed references. Each produced <see cref="IApiTransportFactory"/> wraps a host-owned
/// <see cref="HttpClient"/> (the source's base URL) with a <see cref="SourceCredentialAuthenticationProvider"/> that
/// resolves the current credential from the <see cref="SourceCredentialCache"/> per request.
/// </summary>
/// <remarks>
/// The output of <see cref="CreateApiSources"/> is exactly the map a host hands to a
/// <c>WorkflowTransportRegistry</c> (the seam that binds a workflow's declared sources to real endpoints), so
/// secure-by-default credential resolution drops into the existing runner binding with no change to the execution
/// path — and a resumed run re-binds through the same registry, picking up the current credential (§13.3).
/// </remarks>
public static class SourceCredentialTransports
{
    /// <summary>Creates an <see cref="IApiTransportFactory"/> for one source whose transports authenticate from the
    /// credential cache.</summary>
    /// <param name="httpClient">The host-owned client whose <see cref="HttpClient.BaseAddress"/> is the source's base
    /// URL. The host owns its lifetime.</param>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="cache">The runner credential cache.</param>
    /// <returns>The transport factory.</returns>
    public static IApiTransportFactory CreateApiTransportFactory(HttpClient httpClient, string sourceName, string environment, SourceCredentialCache cache)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(cache);
        return new HttpClientApiTransportFactory(httpClient, new SourceCredentialAuthenticationProvider(cache, sourceName, environment));
    }

    /// <summary>Builds the per-source <see cref="IApiTransportFactory"/> map (keyed by source name) a host supplies to a
    /// <c>WorkflowTransportRegistry</c>, each authenticating from the credential cache for the given environment.</summary>
    /// <param name="sourceClients">The host-owned clients, keyed by the source name the workflow declares; each client's
    /// <see cref="HttpClient.BaseAddress"/> is that source's base URL.</param>
    /// <param name="environment">The deployment environment all the sources bind for.</param>
    /// <param name="cache">The runner credential cache.</param>
    /// <returns>The source-name → transport-factory map.</returns>
    public static IReadOnlyDictionary<string, IApiTransportFactory> CreateApiSources(IReadOnlyDictionary<string, HttpClient> sourceClients, string environment, SourceCredentialCache cache)
    {
        ArgumentNullException.ThrowIfNull(sourceClients);
        ArgumentNullException.ThrowIfNull(cache);
        var map = new Dictionary<string, IApiTransportFactory>(sourceClients.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, HttpClient> source in sourceClients)
        {
            map[source.Key] = CreateApiTransportFactory(source.Value, source.Key, environment, cache);
        }

        return map;
    }
}