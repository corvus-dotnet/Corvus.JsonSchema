// <copyright file="SourceCredentialApiTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// Wraps a source's <see cref="IApiTransport"/> so a <strong>runtime</strong> credential rejection — an HTTP
/// <c>401</c>/<c>403</c> on a call the runner authenticated — becomes the same typed, resumable
/// <c>credentials-expired</c> fault as a bind-time expiry (design §13.3). It is the <em>reactive</em> counterpart of
/// the cache's proactive expiry check: a secret revoked or rotated out-of-band (so it isn't "expired" by date but the
/// source still rejects it) faults the run instead of failing opaquely, and an operator rotates and resumes.
/// </summary>
/// <remarks>
/// <para>The fault is raised only when the run is actually <em>entitled</em> to a binding for the source (a credential
/// was applied); an unauthenticated source's 401/403 is left to its ordinary step success criteria, since that is not a
/// credential rejection. The entitlement check is a warm cache read (the request already resolved it) taken only on the
/// exceptional 401/403 branch — the 2xx path adds just one status comparison.</para>
/// <para>401/403 is a heuristic for "the source rejected the credential"; a 403 that is really resource-level
/// authorization will also fault, but the fault is resumable, so it is recoverable.</para>
/// </remarks>
public sealed class SourceCredentialApiTransport : IApiTransport
{
    private readonly IApiTransport inner;
    private readonly SourceCredentialCache cache;
    private readonly string sourceName;
    private readonly string environment;
    private readonly SecurityTagSet runTags;
    private readonly string runTagsKey;

    /// <summary>Initializes a new instance of the <see cref="SourceCredentialApiTransport"/> class.</summary>
    /// <param name="inner">The transport that actually sends the request (typically credential-authenticating).</param>
    /// <param name="cache">The runner credential cache, consulted to confirm the run applied a credential.</param>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="runTags">The run's own security tags (§14.2), fixed at transport-bind time.</param>
    public SourceCredentialApiTransport(IApiTransport inner, SourceCredentialCache cache, string sourceName, string environment, SecurityTagSet runTags = default)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        this.inner = inner;
        this.cache = cache;
        this.sourceName = sourceName;
        this.environment = environment;
        this.runTags = runTags;
        this.runTagsKey = SourceCredentialKey.CanonicalTags(runTags);
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => this.CheckAsync(this.inner.SendAsync<TRequest, TResponse>(request, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse>
        => this.CheckAsync(this.inner.SendAsync<TRequest, TBody, TResponse>(request, body, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => this.CheckAsync(this.inner.SendAsync<TRequest, TResponse>(request, body, contentType, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => this.CheckAsync(this.inner.SendAsync<TRequest, TResponse>(request, bodyWriter, contentType, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => this.inner.DisposeAsync();

    private async ValueTask<TResponse> CheckAsync<TResponse>(ValueTask<TResponse> pending, CancellationToken cancellationToken)
        where TResponse : struct, IApiResponse<TResponse>
    {
        TResponse response = await pending.ConfigureAwait(false);
        if (response.StatusCode is 401 or 403)
        {
            IHttpAuthenticationProvider? applied = await this.cache.GetAsync(this.sourceName, this.environment, this.runTagsKey, this.runTags, cancellationToken).ConfigureAwait(false);
            if (applied is not null)
            {
                await response.DisposeAsync().ConfigureAwait(false);
                throw new SourceCredentialExpiredException(
                    this.sourceName,
                    $"The source '{this.sourceName}' rejected its credential (HTTP {response.StatusCode}); rotate it in the secret store and resume the run.");
            }
        }

        return response;
    }
}