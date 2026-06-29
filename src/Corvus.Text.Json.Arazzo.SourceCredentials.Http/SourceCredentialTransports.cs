// <copyright file="SourceCredentialTransports.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography.X509Certificates;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// Composes the §13 source-credential cache with the HTTP transport so a runner's per-source transports authenticate
/// from operator-managed references — and, crucially, only with the credential binding the <em>run</em> is entitled to
/// use (§13/§14.2). Each produced transport wraps a host-owned <see cref="HttpClient"/> (the source's base URL) with a
/// <see cref="SourceCredentialAuthenticationProvider"/> bound to the run's security tags, which resolves the entitled
/// credential from the <see cref="SourceCredentialCache"/> per request.
/// </summary>
/// <remarks>
/// <see cref="CreateBinder"/> returns a <see cref="WorkflowTransportBinder"/> a host installs in place of a static
/// <c>WorkflowTransportRegistry</c>: it is invoked per run with that run's tags, so secure-by-default, tenant-scoped
/// credential resolution drops into the existing runner binding seam — and a resumed run re-binds through it, picking up
/// the current credential it is entitled to (§13.3). A run entitled to no binding for a source is left unauthenticated.
/// </remarks>
public static class SourceCredentialTransports
{
    /// <summary>Creates an <see cref="IApiTransportFactory"/> for one source whose transports authenticate from the
    /// credential cache, scoped to a run's tags.</summary>
    /// <param name="httpClient">The host-owned client whose <see cref="HttpClient.BaseAddress"/> is the source's base
    /// URL. The host owns its lifetime.</param>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="cache">The runner credential cache.</param>
    /// <param name="runTags">The run's own security tags (§14.2), so only the entitled binding is applied.</param>
    /// <returns>The transport factory.</returns>
    public static IApiTransportFactory CreateApiTransportFactory(HttpClient httpClient, string sourceName, string environment, SourceCredentialCache cache, SecurityTagSet runTags = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(cache);

        // Inner: applies the entitled credential to each request (§13.4 warm path) and resolves the binding's
        // per-environment base URL override (§8) from the same cached read. Outer: turns a runtime 401/403 on an
        // authenticated call into a typed, resumable credentials-expired fault (§13.3 reactive path).
        string runTagsKey = SourceCredentialKey.CanonicalTags(runTags);
        var authenticating = new HttpClientApiTransportFactory(
            httpClient,
            new SourceCredentialAuthenticationProvider(cache, sourceName, environment, runTags),
            baseUrlOverride: ct => cache.GetBaseUrlAsync(sourceName, environment, runTagsKey, runTags, ct));
        return new SourceCredentialApiTransportFactory(authenticating, cache, sourceName, environment, runTags);
    }

    private sealed class SourceCredentialApiTransportFactory(IApiTransportFactory inner, SourceCredentialCache cache, string sourceName, string environment, SecurityTagSet runTags) : IApiTransportFactory
    {
        public IApiTransport CreateTransport()
            => new SourceCredentialApiTransport(inner.CreateTransport(), cache, sourceName, environment, runTags);
    }

    /// <summary>Builds a <see cref="WorkflowTransportBinder"/> that, per run, binds each of the workflow's declared API
    /// sources to a transport authenticating from the credential cache for the run's tags. Install this as the host's
    /// transport binder so source credential resolution is entitled per run.</summary>
    /// <param name="sourceClients">The host-owned clients, keyed by the source name the workflow declares; each client's
    /// <see cref="HttpClient.BaseAddress"/> is that source's base URL.</param>
    /// <param name="environment">The deployment environment all the sources bind for.</param>
    /// <param name="cache">The runner credential cache.</param>
    /// <param name="messageTransport">The shared message transport for AsyncAPI channel steps, or <see langword="null"/>
    /// if the host binds no message workflows.</param>
    /// <returns>The binder.</returns>
    public static WorkflowTransportBinder CreateBinder(IReadOnlyDictionary<string, HttpClient> sourceClients, string environment, SourceCredentialCache cache, IMessageTransport? messageTransport = null)
    {
        ArgumentNullException.ThrowIfNull(sourceClients);
        ArgumentNullException.ThrowIfNull(cache);
        return (WorkflowDescriptor descriptor, SecurityTagSet runTags) =>
        {
            var apiTransports = new Dictionary<string, IApiTransport>(descriptor.Sources.Count, StringComparer.Ordinal);
            foreach (string source in descriptor.Sources)
            {
                if (!sourceClients.TryGetValue(source, out HttpClient? client))
                {
                    throw new WorkflowTransportBindingException(
                        $"Workflow '{descriptor.WorkflowId}' requires API source '{source}', which has no configured transport binding.");
                }

                apiTransports[source] = CreateApiTransportFactory(client, source, environment, cache, runTags).CreateTransport();
            }

            if (descriptor.NeedsMessageTransport && messageTransport is null)
            {
                throw new WorkflowTransportBindingException(
                    $"Workflow '{descriptor.WorkflowId}' requires a message transport, but none is configured.");
            }

            return new WorkflowTransports(apiTransports, descriptor.NeedsMessageTransport ? messageTransport : null);
        };
    }

    /// <summary>Builds the host-owned <see cref="HttpClient"/> for one source, configuring an mTLS client certificate on
    /// its handler when the source's binding is <see cref="SourceCredentialKind.Mtls"/> (design §13.1). Unlike the
    /// header-based kinds — which the <see cref="SourceCredentialCache"/> applies per request — an mTLS certificate is
    /// connection-level (the TLS handshake), so it is resolved once here and configured on the client's handler. The
    /// host installs the returned clients in the dictionary it passes to <see cref="CreateBinder"/>.</summary>
    /// <param name="store">The credential store the (source, environment) binding is read from.</param>
    /// <param name="factory">The provider factory that resolves the certificate references to a live certificate.</param>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="baseAddress">The source's base URL to set on the client, or <see langword="null"/> to leave it unset.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The host-owned client; disposing it disposes the handler and (for mTLS) the client certificate.</returns>
    /// <remarks>An mTLS binding is connection-scoped, never usage-scoped (it authenticates the deployment, not a run), so
    /// the binding is resolved for shared usage (<see cref="SecurityTagSet.Empty"/>); the control plane refuses to create
    /// a usage-scoped mTLS binding, so there is exactly one shared certificate per (source, environment).</remarks>
    public static async ValueTask<HttpClient> CreateSourceHttpClientAsync(ISourceCredentialStore store, SourceCredentialProviderFactory factory, string sourceName, string environment, Uri? baseAddress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);

        X509Certificate2? clientCertificate = null;
        using (ParsedJsonDocument<SourceCredentialBinding>? binding = await store.ResolveForUsageAsync(sourceName, environment, SecurityTagSet.Empty, cancellationToken).ConfigureAwait(false))
        {
            if (binding is not null && binding.RootElement.AuthKindValue == SourceCredentialKind.Mtls)
            {
                clientCertificate = await factory.ResolveClientCertificateAsync(binding.RootElement, cancellationToken).ConfigureAwait(false);
            }
        }

        HttpClient client;
        if (clientCertificate is not null)
        {
            var sslHandler = new SocketsHttpHandler { SslOptions = { ClientCertificates = new X509CertificateCollection { clientCertificate } } };
            client = new HttpClient(new ClientCertificateOwningHandler(sslHandler, clientCertificate), disposeHandler: true);
        }
        else
        {
            client = new HttpClient();
        }

        if (baseAddress is not null)
        {
            client.BaseAddress = baseAddress;
        }

        return client;
    }

    // Owns the lifetime of the mTLS client certificate alongside the handler: SocketsHttpHandler does not dispose the
    // certificate it presents, so disposing the client (which disposes this delegating handler and its inner handler)
    // also disposes the certificate, scrubbing its private key.
    private sealed class ClientCertificateOwningHandler(SocketsHttpHandler inner, X509Certificate2 certificate) : DelegatingHandler(inner)
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                certificate.Dispose();
            }
        }
    }
}