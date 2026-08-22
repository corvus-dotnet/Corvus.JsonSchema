// <copyright file="RedirectHardeningHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> for run-path source clients (P1-4/TB-10) that follows redirects itself so a
/// credential is never carried across an origin boundary. The inner handler MUST be configured with
/// <c>AllowAutoRedirect=false</c>: on a cross-origin auto-redirect the runtime strips only the typed
/// <c>Authorization</c> header, so a custom API-key header survives and, for an mTLS binding, the client certificate is
/// presented to the redirect target on the new connection.
/// </summary>
/// <remarks>
/// <para>A same-origin redirect is followed (re-checking the scheme each hop, up to <see cref="MaxRedirects"/>); a
/// cross-origin redirect is refused, returning the 3xx response unfollowed so no credential header and no client
/// certificate ever reaches another origin.</para>
/// <para>This is deliberately stricter than the source-document fetcher, which drops credentials and follows
/// off-origin: a run-path call authenticates with a shared, host-owned client whose mTLS certificate cannot be dropped
/// per hop (it is presented on every connection the handler opens), and a cross-origin redirect in the middle of a run
/// is anomalous. Refusing closes the leak for the header and the certificate uniformly.</para>
/// <para>Same-origin following is limited to a body-less <c>GET</c>/<c>HEAD</c> so there is no request content to
/// re-buffer and no method to rewrite; any other method, or a request that carries content, is returned unfollowed
/// (the caller sees the redirect as a non-success). The cross-origin leak is closed regardless of method.</para>
/// </remarks>
public sealed class RedirectHardeningHandler : DelegatingHandler
{
    /// <summary>The maximum number of same-origin redirects followed before the last response is returned as-is.</summary>
    public const int MaxRedirects = 5;

    private readonly bool allowInsecureHttp;

    /// <summary>Initializes a new instance of the <see cref="RedirectHardeningHandler"/> class.</summary>
    /// <param name="innerHandler">The inner handler, which MUST have <c>AllowAutoRedirect=false</c> so this handler is
    /// the only thing that follows redirects.</param>
    /// <param name="allowInsecureHttp">Permit a same-origin redirect to an <c>http</c> URL (default: <c>https</c> only,
    /// mirroring the source-fetch scheme policy).</param>
    public RedirectHardeningHandler(HttpMessageHandler innerHandler, bool allowInsecureHttp = false)
        : base(innerHandler)
    {
        this.allowInsecureHttp = allowInsecureHttp;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not { } origin)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        string originAuthority = origin.GetLeftPart(UriPartial.Authority);
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        for (int hop = 0; hop < MaxRedirects; hop++)
        {
            if ((int)response.StatusCode is not (>= 300 and < 400) || response.Headers.Location is not { } location)
            {
                return response;
            }

            Uri next = location.IsAbsoluteUri ? location : new Uri(request.RequestUri!, location);

            // A cross-origin redirect is refused: the request carries the source credential (a custom header) and, for
            // an mTLS binding, the client certificate is presented on the new connection. Neither may reach another
            // origin, so the redirect response is returned unfollowed and nothing is sent off-origin.
            if (!string.Equals(next.GetLeftPart(UriPartial.Authority), originAuthority, StringComparison.OrdinalIgnoreCase))
            {
                return response;
            }

            // Same-origin, but only a body-less GET/HEAD is followed (method preserved, no content to re-buffer). Any
            // other method or a request with content is returned unfollowed.
            if ((request.Method != HttpMethod.Get && request.Method != HttpMethod.Head) || request.Content is not null)
            {
                return response;
            }

            // A same-origin redirect cannot change scheme, but re-check defensively so a downgrade is never followed.
            if (next.Scheme != Uri.UriSchemeHttps && !(this.allowInsecureHttp && next.Scheme == Uri.UriSchemeHttp))
            {
                return response;
            }

            var nextRequest = new HttpRequestMessage(request.Method, next);
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                nextRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Dispose();
            request = nextRequest;
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}