// <copyright file="RunnerApiArtifactSource.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json.Arazzo.Durability.Runner.Client.Models;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// Serves executor artifacts over the runner API, so a runner loads a version's executor holding no catalog
/// credential (ADR 0065). Handed to <see cref="LoaderHostedWorkflowResolver"/> or
/// <see cref="HostedWorkflowResumer"/>, it is indistinguishable from reading the catalog directly.
/// </summary>
/// <remarks>
/// Being served from here is not why the runner trusts what it gets. The assembly is verified against the content
/// hash this also serves, and against the manifest's signature, after the pull. The hash comes from its own operation
/// rather than from inside the package, because a manifest vouching for itself would prove nothing.
/// </remarks>
public sealed class RunnerApiArtifactSource : IWorkflowArtifactSource
{
    private readonly IApiCatalogClient catalog;

    /// <summary>Initializes a new instance of the <see cref="RunnerApiArtifactSource"/> class over a transport.</summary>
    /// <param name="transport">The transport to the runner API host.</param>
    public RunnerApiArtifactSource(IApiTransport transport)
        : this(new ApiCatalogClient(transport))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RunnerApiArtifactSource"/> class over a prepared client.</summary>
    /// <param name="catalog">The catalog client.</param>
    public RunnerApiArtifactSource(IApiCatalogClient catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        this.catalog = catalog;
    }

    /// <inheritdoc/>
    public async ValueTask<string?> GetContentHashAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);

        await using GetHostedVersionResponse response = await this.catalog.GetHostedVersionAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == 404)
        {
            // Not available to this runner, or not there at all. The API answers the same either way, so the resolver
            // gets the same "no such version" it would from a catalog.
            return null;
        }

        return response.StatusCode == 200
            ? (string)response.OkBody.Hash
            : throw new RunnerApiException((System.Net.HttpStatusCode)response.StatusCode, $"The runner API refused to serve version {versionNumber} of '{baseWorkflowId}'.");
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(documentName);

        await using GetVersionDocumentResponse response = await this.catalog.GetVersionDocumentAsync(baseWorkflowId, versionNumber, documentName, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == 404)
        {
            return null;
        }

        if (response.StatusCode != 200 || !response.TryGetOkStream(out Stream? content))
        {
            throw new RunnerApiException((System.Net.HttpStatusCode)response.StatusCode, $"The runner API refused to serve '{documentName}' for version {versionNumber} of '{baseWorkflowId}'.");
        }

        return await ReadAllAsync(content, cancellationToken).ConfigureAwait(false);
    }

    // An executor assembly is megabytes, so the read grows through pooled buffers and the single retained array is
    // sized exactly once at the end. The loader keeps what it is given, which is why this is the one copy that stays.
    private static async ValueTask<ReadOnlyMemory<byte>> ReadAllAsync(Stream content, CancellationToken cancellationToken)
    {
        var buffer = new ArrayBufferWriter<byte>(64 * 1024);
        while (true)
        {
            Memory<byte> block = buffer.GetMemory(64 * 1024);
            int read = await content.ReadAsync(block, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.WrittenMemory.ToArray();
            }

            buffer.Advance(read);
        }
    }
}