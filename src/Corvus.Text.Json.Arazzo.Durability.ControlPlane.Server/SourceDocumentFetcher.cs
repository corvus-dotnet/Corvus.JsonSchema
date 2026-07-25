// <copyright file="SourceDocumentFetcher.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Security.Cryptography;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.SourceCredentials.Http;
using Corvus.Text.Json.Canonicalization;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Corvus.Text.Json.Yaml;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Fetches an OpenAPI/AsyncAPI/Arazzo document from a web endpoint server-side for
/// <c>fetchSourceDocument</c> (workflow-designer design §4.4): no browser CORS, and an optional
/// registered-credential reference authenticates the request through the §13 machinery
/// (<see cref="SourceCredentialProviderFactory"/> — the control plane resolves the reference;
/// secret material never rides the API). The fetched payload parses as JSON or YAML, its type and
/// version detect from the document's own top-level field, and the digest is the repo's canonical
/// convention (SHA-256 lower-hex over the RFC 8785 form).
/// </summary>
/// <remarks>
/// <para><strong>Outbound safety.</strong> The scheme is <c>https</c>-only unless the deployment
/// opts in to <c>http</c> (mirroring the credential token-endpoint guard); the payload is capped at
/// <see cref="MaxDocumentBytes"/> and downloads into a pooled buffer. Network-level destination
/// fencing (private-range/SSRF policy) belongs to the deployment's egress controls — the fetcher
/// deliberately makes no address-based decisions the deployment's network cannot make better.</para>
/// <para><strong>Ownership.</strong> A successful fetch hands back a
/// <see cref="FetchedDocument"/> whose pooled document the caller owns (dispose it, or transfer it
/// to the request workspace via the response body write). The download buffer is pooled and
/// returned before the fetch call completes.</para>
/// </remarks>
public sealed class SourceDocumentFetcher
{
    private const int DefaultCanonicalBufferSize = 64 * 1024;

    // The fetcher follows redirects itself (the fetch client must not auto-redirect); this caps the chain.
    private const int MaxRedirects = 5;

    private readonly HttpClient client;
    private readonly ISourceCredentialStore? credentials;
    private readonly SourceCredentialProviderFactory? credentialProviders;
    private readonly bool allowInsecureHttp;

    /// <summary>Initializes a new instance of the <see cref="SourceDocumentFetcher"/> class.</summary>
    /// <param name="client">The outbound HTTP client. It MUST be configured to <strong>not</strong>
    /// auto-follow redirects (<c>AllowAutoRedirect = false</c>): the fetcher follows redirects itself
    /// so a credential is never carried across an origin boundary. The deployment configures its
    /// handler/egress policy (and should fence outbound destinations at the network layer for SSRF).</param>
    /// <param name="credentials">The credential registry for authenticated fetches; <see langword="null"/> disables the credential option.</param>
    /// <param name="credentialProviders">Builds authentication providers from credential bindings (needs the deployment's secret resolver); <see langword="null"/> disables the credential option.</param>
    /// <param name="allowInsecureHttp">Permit <c>http</c> URLs (default: <c>https</c> only, mirroring §17.5/F5).</param>
    /// <param name="maxDocumentBytes">The payload cap (default 16 MiB).</param>
    public SourceDocumentFetcher(
        HttpClient client,
        ISourceCredentialStore? credentials = null,
        SourceCredentialProviderFactory? credentialProviders = null,
        bool allowInsecureHttp = false,
        int maxDocumentBytes = 16 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
        this.credentials = credentials;
        this.credentialProviders = credentialProviders;
        this.allowInsecureHttp = allowInsecureHttp;
        this.MaxDocumentBytes = maxDocumentBytes;
    }

    /// <summary>The outcome kinds a fetch can report (mapped to problem responses by the handler).</summary>
    public enum FetchOutcome
    {
        /// <summary>The document fetched, parsed, and detected.</summary>
        Success,

        /// <summary>The URL is absent, relative, or not a well-formed absolute URL.</summary>
        InvalidUrl,

        /// <summary>The URL's scheme is not permitted (https required without the deployment opt-in).</summary>
        SchemeNotPermitted,

        /// <summary>A credential was requested but this deployment offers no credentialed fetch.</summary>
        CredentialFetchUnavailable,

        /// <summary>The referenced credential is absent or outside the caller's reach (non-disclosing).</summary>
        CredentialNotFound,

        /// <summary>The credential exists but cannot authenticate this fetch (e.g. an mTLS binding on a shared client).</summary>
        CredentialUnusable,

        /// <summary>A workload binding was targeted at a host it is not scoped to (it declares no <c>baseUrl</c>,
        /// or the URL's host is not the binding's <c>baseUrl</c> host) — the shared secret is not sent elsewhere.</summary>
        CredentialHostNotAllowed,

        /// <summary>The upstream endpoint failed: unreachable, an error status, or over the size cap.</summary>
        UpstreamFailed,

        /// <summary>The payload is neither parseable JSON nor parseable YAML.</summary>
        NotADocument,

        /// <summary>The document declares neither <c>openapi</c>, <c>asyncapi</c>, nor <c>arazzo</c>.</summary>
        NoRecognisableType,
    }

    /// <summary>Gets the payload cap in bytes.</summary>
    public int MaxDocumentBytes { get; }

    /// <summary>Fetches, parses, and detects the document.</summary>
    /// <param name="url">The document URL.</param>
    /// <param name="reach">The caller's row-access grant (gates the credential reference lookup).</param>
    /// <param name="credentialSourceName">The credential reference's source name, when authenticating with a §13 binding.</param>
    /// <param name="credentialEnvironment">The credential reference's environment, when authenticating with a §13 binding.</param>
    /// <param name="authHeader">A single request header for this fetch (ADR 0052: a connected
    /// provider's user token as <c>Authorization: Bearer …</c>, or a one-shot header credential in
    /// whatever scheme the endpoint takes). Attached to the one outbound request, never stored,
    /// never logged. Exclusive with the credential reference (the handler enforces it).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome, a human-readable detail for failures, and — on success — the fetched document (the caller owns it).</returns>
    public async ValueTask<(FetchOutcome Outcome, string? Detail, FetchedDocument? Document)> FetchAsync(
        string url,
        AccessContext reach,
        string? credentialSourceName,
        string? credentialEnvironment,
        (string Name, string Value)? authHeader,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return (FetchOutcome.InvalidUrl, $"'{url}' is not an absolute URL.", null);
        }

        if (uri.Scheme != Uri.UriSchemeHttps && !(this.allowInsecureHttp && uri.Scheme == Uri.UriSchemeHttp))
        {
            return (FetchOutcome.SchemeNotPermitted, $"The scheme '{uri.Scheme}' is not permitted; use https{(this.allowInsecureHttp ? " or http" : string.Empty)}.", null);
        }

        // Resolve the workload binding once and scope it to its own host. A §13 binding is a WORKLOAD
        // credential (what a runner presents to the source's API); it may be presented only to the
        // source's declared endpoint (its non-secret `baseUrl` host), never to an arbitrary fetch URL —
        // otherwise a caller with reach to the binding could exfiltrate its shared secret to any host.
        IHttpAuthenticationProvider? bindingProvider = null;
        if (credentialSourceName is not null)
        {
            if (this.credentials is null || this.credentialProviders is null)
            {
                return (FetchOutcome.CredentialFetchUnavailable, "This deployment does not offer credentialed fetches.", null);
            }

            using ParsedJsonDocument<SourceCredentialBinding>? binding =
                await this.credentials.GetAsync(credentialSourceName, credentialEnvironment ?? string.Empty, reach, cancellationToken).ConfigureAwait(false);
            if (binding is not { } b)
            {
                return (FetchOutcome.CredentialNotFound, $"No credential for source '{credentialSourceName}' in environment '{credentialEnvironment}' exists, or it is outside your reach.", null);
            }

            if (b.RootElement.AuthKindValue == SourceCredentialKind.Mtls)
            {
                return (FetchOutcome.CredentialUnusable, "An mTLS credential binds at the connection level and cannot authenticate the shared fetch client.", null);
            }

            Uri? baseUrl = b.RootElement.BaseUrlOverrideOrNull;
            if (baseUrl is null)
            {
                return (FetchOutcome.CredentialHostNotAllowed, $"The credential for source '{credentialSourceName}' declares no 'baseUrl', so it cannot be scoped to a fetch host; add a baseUrl to the credential, or fetch with a one-shot secret.", null);
            }

            if (!string.Equals(baseUrl.Host, uri.Host, StringComparison.OrdinalIgnoreCase) || baseUrl.Port != uri.Port)
            {
                return (FetchOutcome.CredentialHostNotAllowed, $"The credential for source '{credentialSourceName}' is scoped to host '{baseUrl.Authority}', not '{uri.Authority}'; the workload credential is not sent to other hosts. Use a one-shot secret for this URL.", null);
            }

            bindingProvider = await this.credentialProviders.CreateAsync(b.RootElement, cancellationToken).ConfigureAwait(false);
        }

        // The fetcher follows redirects itself so a credential is NEVER carried across an origin
        // boundary: the runtime strips the standard Authorization header on a cross-origin redirect,
        // but a custom header (an API key) is not stripped, so following redirects safely — dropping
        // credentials off-origin and re-checking the scheme each hop — is the fetcher's job. The
        // deployment's fetch HttpClient MUST therefore be configured with AllowAutoRedirect=false.
        string originAuthority = uri.GetLeftPart(UriPartial.Authority);
        Uri current = uri;
        HttpResponseMessage? response = null;
        for (int hop = 0; ; hop++)
        {
            if (!ReferenceEquals(current, uri)
                && current.Scheme != Uri.UriSchemeHttps && !(this.allowInsecureHttp && current.Scheme == Uri.UriSchemeHttp))
            {
                return (FetchOutcome.SchemeNotPermitted, $"A redirect to scheme '{current.Scheme}' is not permitted.", null);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            bool sameOrigin = string.Equals(current.GetLeftPart(UriPartial.Authority), originAuthority, StringComparison.OrdinalIgnoreCase);
            bool credentialed = false;
            if (sameOrigin)
            {
                if (authHeader is { } h)
                {
                    request.Headers.TryAddWithoutValidation(h.Name, h.Value);
                    credentialed = true;
                }

                if (bindingProvider is not null)
                {
                    await bindingProvider.AuthenticateAsync(request, cancellationToken).ConfigureAwait(false);
                    credentialed = true;
                }
            }

            try
            {
                response = await this.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                return (FetchOutcome.UpstreamFailed, $"The endpoint could not be reached: {ex.Message}", null);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return (FetchOutcome.UpstreamFailed, "The endpoint timed out.", null);
            }

            // Defense in depth: if a misconfigured client auto-followed a redirect while we attached a
            // credential, the credential may have crossed origins — fail closed and name the misconfig.
            if (credentialed && response.RequestMessage?.RequestUri is { } finalUri
                && !string.Equals(finalUri.GetLeftPart(UriPartial.Authority), current.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                response.Dispose();
                return (FetchOutcome.UpstreamFailed, "The fetch HttpClient auto-followed a redirect while presenting a credential; configure it with AllowAutoRedirect=false so the fetcher follows redirects without leaking credentials.", null);
            }

            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location && hop < MaxRedirects)
            {
                Uri next = location.IsAbsoluteUri ? location : new Uri(current, location);
                response.Dispose();
                current = next;
                continue;
            }

            break;
        }

        using HttpResponseMessage finalResponse = response!;
        {
            if (!finalResponse.IsSuccessStatusCode)
            {
                return (FetchOutcome.UpstreamFailed, $"The endpoint returned {(int)finalResponse.StatusCode}.", null);
            }

            string? contentType = finalResponse.Content.Headers.ContentType?.MediaType;

            // Download into a pooled buffer, capped; the buffer is returned before this method exits
            // (the parse below copies into the pooled document's own storage).
            byte[] rented = ArrayPool<byte>.Shared.Rent(Math.Min(this.MaxDocumentBytes, 64 * 1024));
            try
            {
                int length = 0;
                using (Stream stream = await finalResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (true)
                    {
                        if (length == rented.Length)
                        {
                            if (length >= this.MaxDocumentBytes)
                            {
                                return (FetchOutcome.UpstreamFailed, $"The payload exceeds the {this.MaxDocumentBytes / (1024 * 1024)} MiB cap.", null);
                            }

                            byte[] grown = ArrayPool<byte>.Shared.Rent(Math.Min(this.MaxDocumentBytes, rented.Length * 2));
                            rented.AsSpan(0, length).CopyTo(grown);
                            ArrayPool<byte>.Shared.Return(rented);
                            rented = grown;
                        }

                        int read = await stream.ReadAsync(rented.AsMemory(length, rented.Length - length), cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                        {
                            break;
                        }

                        length += read;
                        if (length > this.MaxDocumentBytes)
                        {
                            return (FetchOutcome.UpstreamFailed, $"The payload exceeds the {this.MaxDocumentBytes / (1024 * 1024)} MiB cap.", null);
                        }
                    }
                }

                return ParseAndDetect(rented.AsMemory(0, length), contentType, url);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    // Parses the payload (JSON first unless the content type says YAML; YAML as the fallback either
    // way), detects the type/version from the document's own top-level field, and digests the
    // canonical form. The returned pooled document owns its own storage — the download buffer is free
    // to return.
    private static (FetchOutcome Outcome, string? Detail, FetchedDocument? Document) ParseAndDetect(ReadOnlyMemory<byte> payload, string? contentType, string url)
    {
        bool looksYaml = contentType is not null && (contentType.Contains("yaml", StringComparison.OrdinalIgnoreCase) || contentType.Contains("yml", StringComparison.OrdinalIgnoreCase));
        ParsedJsonDocument<JsonElement>? parsed = looksYaml ? null : TryParseJson(payload);
        parsed ??= TryParseYaml(payload);
        if (parsed is not { } document)
        {
            return (FetchOutcome.NotADocument, "The payload is neither parseable JSON nor parseable YAML.", null);
        }

        JsonElement root = document.RootElement;
        string? type = WorkspaceSourceJson.DetectDocumentType(root);
        if (type is null)
        {
            document.Dispose();
            return (FetchOutcome.NoRecognisableType, "The document declares neither openapi, asyncapi, nor arazzo.", null);
        }

        string? version = root.TryGetProperty(type, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        return (FetchOutcome.Success, null, new FetchedDocument(document, url, type, version, HashCanonical(root, payload.Length), contentType));
    }

    private static ParsedJsonDocument<JsonElement>? TryParseJson(ReadOnlyMemory<byte> payload)
    {
        try
        {
            return ParsedJsonDocument<JsonElement>.Parse(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ParsedJsonDocument<JsonElement>? TryParseYaml(ReadOnlyMemory<byte> payload)
    {
        try
        {
            return YamlDocument.Parse<JsonElement>(payload);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The YAML reader's exception surface is broad; a fetch of arbitrary bytes must not throw.
            return null;
        }
    }

    // The repo's digest convention (WorkflowPackage.HashCanonical): RFC 8785 canonical form hashed in
    // a pooled buffer, SHA-256, lower-hex.
    private static string HashCanonical(in JsonElement element, int sizeHint)
    {
        int bufferSize = Math.Max(DefaultCanonicalBufferSize, sizeHint);
        while (true)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                if (JsonCanonicalizer.TryCanonicalize(element, rented, out int written))
                {
                    Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
                    SHA256.HashData(rented.AsSpan(0, written), digest);
                    return Convert.ToHexStringLower(digest);
                }

                bufferSize = checked(bufferSize * 2);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>A successful fetch: the pooled document (the caller owns it) plus what was detected about it.</summary>
    public sealed class FetchedDocument : IDisposable
    {
        internal FetchedDocument(ParsedJsonDocument<JsonElement> document, string url, string type, string? version, string digest, string? contentType)
        {
            this.Document = document;
            this.Url = url;
            this.Type = type;
            this.Version = version;
            this.Digest = digest;
            this.ContentType = contentType;
        }

        /// <summary>Gets the fetched document (pooled; dispose or transfer to the request workspace).</summary>
        public ParsedJsonDocument<JsonElement> Document { get; }

        /// <summary>Gets the fetched URL.</summary>
        public string Url { get; }

        /// <summary>Gets the detected type (<c>openapi</c> | <c>asyncapi</c> | <c>arazzo</c>).</summary>
        public string Type { get; }

        /// <summary>Gets the declared spec version, when the document carries one.</summary>
        public string? Version { get; }

        /// <summary>Gets the SHA-256 (lower-hex) of the document's RFC 8785 canonical form.</summary>
        public string Digest { get; }

        /// <summary>Gets the upstream content type, when one was sent.</summary>
        public string? ContentType { get; }

        /// <inheritdoc/>
        public void Dispose() => this.Document.Dispose();
    }
}