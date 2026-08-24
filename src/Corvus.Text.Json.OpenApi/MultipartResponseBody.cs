// <copyright file="MultipartResponseBody.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Holds a client-received <c>multipart/form-data</c> response body until it is read.
/// The body streams from the live response: the generated
/// <c>Get{Accessor}MultipartAsync</c> accessor begins a
/// <see cref="MultipartStreamingDriver"/> over it exactly once, and the binary part
/// handles it hands out read directly off the wire in wire order.
/// </summary>
/// <remarks>
/// <para>
/// This is a class deliberately: generated response types are mutable structs, and an
/// async accessor mutates a state-machine copy, so streaming state must live on the
/// heap where every copy of the response sees it. The response disposes this box (and
/// with it the driver and the parsed body document) when the response is disposed;
/// part streams are valid until then.
/// </para>
/// </remarks>
public sealed class MultipartResponseBody
{
    private readonly Stream contentStream;
    private readonly IResponseHeaders? responseHeaders;
    private MultipartStreamingDriver? driver;
    private IDisposable? bodyDocument;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultipartResponseBody"/> class.
    /// </summary>
    /// <param name="contentStream">The live response content stream. The response's owner controls its lifetime.</param>
    /// <param name="responseHeaders">The response headers; the full Content-Type header supplies the multipart boundary.</param>
    public MultipartResponseBody(Stream contentStream, IResponseHeaders? responseHeaders)
    {
        this.contentStream = contentStream;
        this.responseHeaders = responseHeaders;
    }

    /// <summary>
    /// Begins the streaming driver over the response body. Called by the generated
    /// accessor; the body can be read only once.
    /// </summary>
    /// <param name="binaryPartNames">The binary part names declared by the response's schema.</param>
    /// <param name="maxNonBinaryPartsLength">The maximum total bytes of non-binary parts accumulated for the typed body.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The driver, positioned with the typed-body projection complete.</returns>
    /// <exception cref="InvalidOperationException">The body has already been read.</exception>
    public async ValueTask<MultipartStreamingDriver> BeginAsync(string[] binaryPartNames, long maxNonBinaryPartsLength, CancellationToken cancellationToken = default)
    {
        if (this.driver is not null)
        {
            ThrowHelper.ThrowMultipartResponseAlreadyRead();
        }

        string? contentType = null;
        this.responseHeaders?.TryGetValue("Content-Type", out contentType);
        this.driver = await MultipartStreamingDriver.BeginAsync(this.contentStream, contentType, binaryPartNames, maxNonBinaryPartsLength, cancellationToken).ConfigureAwait(false);
        return this.driver;
    }

    /// <summary>
    /// Transfers ownership of the parsed typed-body document to this box, which
    /// disposes it with the driver when the response is disposed.
    /// </summary>
    /// <param name="document">The parsed document backing the typed body.</param>
    public void TakeBodyDocument(IDisposable document) => this.bodyDocument = document;

    /// <summary>
    /// Releases the driver and the parsed body document. Called by the generated
    /// response's <c>DisposeAsync</c>.
    /// </summary>
    /// <returns>A value task that completes when disposal is done.</returns>
    public async ValueTask DisposeAsync()
    {
        this.bodyDocument?.Dispose();
        this.bodyDocument = null;
        if (this.driver is { } d)
        {
            this.driver = null;
            await d.DisposeAsync().ConfigureAwait(false);
        }
    }
}