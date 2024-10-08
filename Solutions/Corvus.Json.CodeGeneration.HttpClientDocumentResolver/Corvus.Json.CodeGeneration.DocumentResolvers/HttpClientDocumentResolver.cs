// <copyright file="HttpClientDocumentResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Resolves a <see cref="JsonDocument"/> from an HTTP endpoint.
/// </summary>
public class HttpClientDocumentResolver : IDocumentResolver
{
    private static readonly ReadOnlyMemory<char> LocalHost = "localhost".AsMemory();

    private readonly HttpClient httpClient;
    private readonly Dictionary<string, JsonDocument> documents = [];
    private readonly bool supportLocalhost;
    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientDocumentResolver"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use to resolve the uri.</param>
    /// <param name="supportLocalhost">If true, we support resolving from localhost, otherwise false.</param>
    public HttpClientDocumentResolver(IHttpClientFactory httpClientFactory, bool supportLocalhost = false)
    {
        this.httpClient = httpClientFactory.CreateClient();
        this.supportLocalhost = supportLocalhost;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientDocumentResolver"/> class.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use to resolve the uri.</param>
    public HttpClientDocumentResolver(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    /// <inheritdoc/>
    public bool AddDocument(string uri, JsonDocument document)
    {
        this.CheckDisposed();

        return this.documents.TryAdd(uri, document);
    }

    /// <inheritdoc/>
    public async ValueTask<JsonElement?> TryResolve(JsonReference reference)
    {
        this.CheckDisposed();

        if (!this.supportLocalhost)
        {
            if (IsLocalHost(reference))
            {
                return default;
            }
        }

        string uri = reference.Uri.ToString();
        if (this.documents.TryGetValue(uri, out JsonDocument? result))
        {
            return JsonPointerUtilities.ResolvePointer(result, reference.Fragment);
        }

        try
        {
#if NET8_0_OR_GREATER
            await using Stream stream = await this.httpClient.GetStreamAsync(uri);
#else
            using Stream stream = await this.httpClient.GetStreamAsync(uri);
#endif
            result = await JsonDocument.ParseAsync(stream);
            this.documents.Add(uri, result);
            if (JsonPointerUtilities.TryResolvePointer(result, reference.Fragment, out JsonElement? element))
            {
                return element;
            }

            return default;
        }
        catch (Exception)
        {
            return default;
        }

        static bool IsLocalHost(JsonReference reference)
        {
            JsonReferenceBuilder builder = reference.AsBuilder();
            return builder.Host.SequenceEqual(LocalHost.Span);
        }
    }

    /// <inheritdoc/>
    public void Reset()
    {
        this.CheckDisposed();

        this.DisposeDocumentsAndClear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put clean-up code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Implements the dispose pattern.
    /// </summary>
    /// <param name="disposing">True if we are disposing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this.DisposeDocumentsAndClear();
            }

            this.disposedValue = true;
        }
    }

    private void DisposeDocumentsAndClear()
    {
        foreach (KeyValuePair<string, JsonDocument> document in this.documents)
        {
            document.Value.Dispose();
        }

        this.documents.Clear();
    }

    private void CheckDisposed()
    {
#if NET8_0_OR_GREATER
        ObjectDisposedException.ThrowIf(this.disposedValue, this);
#else
        if (this.disposedValue)
        {
            throw new ObjectDisposedException(nameof(CompoundDocumentResolver));
        }
#endif
    }
}