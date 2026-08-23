// <copyright file="MultipartStreamingDriver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Drives a streaming multipart request for a generated endpoint: the non-binary
/// parts are read into a JSON projection for the typed body, and binary parts are
/// handed to the handler as forward-only streams in wire order via
/// <see cref="BinaryPartHandle"/>, without ever materializing their content.
/// </summary>
/// <remarks>
/// <para>
/// Under <see cref="MultipartBinaryOrdering.RequireBinaryLast"/>, projection building
/// stops at the first binary part; a non-binary part encountered after it raises
/// <see cref="MultipartOrderingException"/>, which generated endpoints map to 400.
/// </para>
/// <para>
/// Binary parts are consumed in wire order: opening a handle drains any earlier
/// unconsumed binary parts permanently, and opening a part that has already been
/// passed throws. The generated endpoint disposes the driver in its
/// <c>finally</c>; the request stream itself is owned by the host.
/// </para>
/// </remarks>
public sealed class MultipartStreamingDriver : IAsyncDisposable
{
    private readonly StreamingMultipartReader reader;
    private readonly PooledBufferWriter projection;
    private readonly byte[] boundaryBuffer;
    private readonly HashSet<string> passedParts = new(StringComparer.Ordinal);

    private string? pendingBinaryPartName;
    private bool finished;

    private MultipartStreamingDriver(StreamingMultipartReader reader, PooledBufferWriter projection, byte[] boundaryBuffer)
    {
        this.reader = reader;
        this.projection = projection;
        this.boundaryBuffer = boundaryBuffer;
    }

    /// <summary>
    /// Gets the UTF-8 JSON projection of the body's non-binary parts. The memory is
    /// pooled and valid until the driver is disposed.
    /// </summary>
    public ReadOnlyMemory<byte> ProjectionUtf8Json => this.projection.WrittenMemory;

    /// <summary>
    /// Begins driving a streaming multipart body: reads the non-binary parts into the
    /// JSON projection, stopping at the first binary part.
    /// </summary>
    /// <param name="body">The request body stream. The driver does not dispose it.</param>
    /// <param name="contentType">The request Content-Type header (carries the boundary).</param>
    /// <param name="options">The registration-time server options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The driver, positioned with the typed-body projection complete.</returns>
    /// <exception cref="RequestBodyTooLargeException">The non-binary parts exceeded <see cref="ApiServerOptions.MaxNonBinaryPartsLength"/>.</exception>
    /// <exception cref="InvalidDataException">The body is not well-formed multipart content.</exception>
    public static async ValueTask<MultipartStreamingDriver> BeginAsync(
        Stream body,
        string? contentType,
        ApiServerOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.MultipartBinaryOrdering != MultipartBinaryOrdering.RequireBinaryLast)
        {
            // SpoolOutOfOrder arrives with the spooling infrastructure.
            throw new NotSupportedException("SpoolOutOfOrder is not implemented yet.");
        }

        // Encode the Content-Type and extract the boundary into a rented buffer that
        // outlives this method (the reader keeps only its own copy, but the extract
        // works over spans, so encode first).
        int byteCount = contentType is not null
            ? System.Text.Encoding.UTF8.GetByteCount(contentType)
            : 0;
        byte[] ctBuffer = FormFieldReader.Rent(Math.Max(byteCount, 1));
        StreamingMultipartReader? reader = null;
        PooledBufferWriter? projection = null;
        try
        {
            if (contentType is not null)
            {
                System.Text.Encoding.UTF8.GetBytes(contentType, 0, contentType.Length, ctBuffer, 0);
            }

            if (!MultipartFormReader.TryExtractBoundary(ctBuffer.AsSpan(0, byteCount), out ReadOnlySpan<byte> boundarySpan))
            {
                ThrowHelper.ThrowMultipartBoundaryNotFound();
            }

            reader = new StreamingMultipartReader(body, boundarySpan);
            projection = new PooledBufferWriter(4096);

            MultipartStreamingDriver driver = new(reader, projection, ctBuffer);
            await driver.BuildProjectionAsync(options.MaxNonBinaryPartsLength, cancellationToken).ConfigureAwait(false);
            return driver;
        }
        catch
        {
            projection?.Dispose();
            if (reader is not null)
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }

            FormFieldReader.Return(ctBuffer);
            throw;
        }
    }

    /// <summary>
    /// Creates a handle for the named binary part. Opening the handle advances the
    /// wire to that part.
    /// </summary>
    /// <param name="partName">The part's form field name.</param>
    /// <param name="required">Whether the part is required by the spec; opening a
    /// missing required part throws <see cref="RequiredBinaryPartMissingException"/>.</param>
    /// <returns>The handle.</returns>
    public BinaryPartHandle GetHandle(string partName, bool required) => new(this, partName, required);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        this.projection.Dispose();
        await this.reader.DisposeAsync().ConfigureAwait(false);
        FormFieldReader.Return(this.boundaryBuffer);
    }

    internal async ValueTask<Stream?> OpenPartAsync(string partName, bool required, CancellationToken cancellationToken)
    {
        if (this.passedParts.Contains(partName))
        {
            ThrowHelper.ThrowBinaryPartAlreadyPassed(partName);
        }

        while (true)
        {
            if (this.pendingBinaryPartName is null)
            {
                if (this.finished || !await this.reader.MoveNextPartAsync(cancellationToken).ConfigureAwait(false))
                {
                    this.finished = true;
                    if (required)
                    {
                        ThrowHelper.ThrowRequiredBinaryPartMissing(partName);
                    }

                    return null;
                }

                if (!this.reader.CurrentIsBinary)
                {
                    // RequireBinaryLast: the typed body was completed before the first
                    // binary part, so a trailing non-binary part cannot be represented.
                    ThrowHelper.ThrowMultipartOrderingViolation();
                }

                this.pendingBinaryPartName = System.Text.Encoding.UTF8.GetString(this.reader.CurrentName);
            }

            if (string.Equals(this.pendingBinaryPartName, partName, StringComparison.Ordinal))
            {
                this.passedParts.Add(partName);
                this.pendingBinaryPartName = null;
                return this.reader.CurrentBodyStream;
            }

            // A different binary part is next on the wire: it is passed over
            // permanently (the next MoveNext drains it).
            this.passedParts.Add(this.pendingBinaryPartName);
            this.pendingBinaryPartName = null;
        }
    }

    private async ValueTask BuildProjectionAsync(long maxNonBinaryBytes, CancellationToken cancellationToken)
    {
        using Utf8JsonWriter writer = new(this.projection);
        writer.WriteStartObject();

        long budget = maxNonBinaryBytes;
        byte[]? scratch = null;
        try
        {
            while (await this.reader.MoveNextPartAsync(cancellationToken).ConfigureAwait(false))
            {
                if (this.reader.CurrentIsBinary)
                {
                    // The typed body is complete; the binary tail is consumed via handles.
                    this.pendingBinaryPartName = System.Text.Encoding.UTF8.GetString(this.reader.CurrentName);
                    break;
                }

                if (this.reader.CurrentName.IsEmpty)
                {
                    // Unnamed non-binary part: nothing to project it as; skip it.
                    continue;
                }

                // Read the part body into scratch, bounded by the remaining budget.
                int length = 0;
                scratch ??= FormFieldReader.Rent(4096);
                Stream partBody = this.reader.CurrentBodyStream;
                while (true)
                {
                    if (length == scratch.Length)
                    {
                        byte[] larger = FormFieldReader.Rent(scratch.Length * 2);
                        scratch.AsSpan(0, length).CopyTo(larger);
                        FormFieldReader.Return(scratch);
                        scratch = larger;
                    }

                    int read = await partBody.ReadAsync(scratch.AsMemory(length, scratch.Length - length), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    length += read;
                    budget -= read;
                    if (budget < 0)
                    {
                        ThrowHelper.ThrowRequestBodyTooLarge(maxNonBinaryBytes);
                    }
                }

                writer.WritePropertyName(this.reader.CurrentName);
                ReadOnlySpan<byte> value = scratch.AsSpan(0, length);
                if (MultipartFormReader.IsJsonContentType(this.reader.CurrentContentType))
                {
                    if (value.IsEmpty)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        writer.WriteRawValue(value);
                    }
                }
                else
                {
                    FormFieldReader.WriteJsonValue(writer, value);
                }
            }

            writer.WriteEndObject();
            writer.Flush();
        }
        finally
        {
            if (scratch is not null)
            {
                FormFieldReader.Return(scratch);
            }
        }
    }
}