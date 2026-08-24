// <copyright file="MultipartStreamingDriver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Drives a streaming multipart request for a generated endpoint: the non-binary
/// parts are read into a JSON projection for the typed body, and binary parts are
/// handed to the handler via <see cref="BinaryPartHandle"/>.
/// </summary>
/// <remarks>
/// <para>
/// Under <see cref="MultipartBinaryOrdering.RequireBinaryLast"/>, projection building
/// stops at the first binary part, and binary parts stream live off the wire: opening
/// a handle drains any earlier unconsumed binary parts permanently, opening a part
/// that has already been passed throws, and a non-binary part encountered after a
/// binary part raises <see cref="MultipartOrderingException"/>, which generated
/// endpoints map to 400.
/// </para>
/// <para>
/// Under <see cref="MultipartBinaryOrdering.SpoolOutOfOrder"/>, the whole body is
/// consumed up front: parts may arrive in any order (as browsers send them), declared
/// binary parts are spooled (pooled memory at or below
/// <see cref="ApiServerOptions.SpoolMemoryThresholdBytes"/>, a delete-on-close
/// temporary file above it), and handles read from the spool in any order, each at
/// most once. Undeclared and duplicate binary parts are drained without being
/// spooled.
/// </para>
/// <para>
/// <c>multipart/mixed</c> bodies (see <see cref="BeginMixedAsync"/>) are positional:
/// the wire order is the schema order, so they always stream live in wire order
/// regardless of the configured ordering policy. Binary parts bind by wire index via
/// <see cref="GetMixedHandle"/>, and a repeating binary tail is consumed through
/// <see cref="GetItemSequence"/>.
/// </para>
/// <para>
/// The generated endpoint disposes the driver in its <c>finally</c>, which releases
/// the spools; the request stream itself is owned by the host. Part streams are valid
/// only for the duration of the handler call.
/// </para>
/// </remarks>
public sealed class MultipartStreamingDriver : IAsyncDisposable
{
    private const int PendingNone = -2;
    private const int PendingUndeclared = -1;

    private readonly StreamingMultipartReader reader;
    private readonly PooledBufferWriter projection;
    private readonly byte[] boundaryBuffer;
    private readonly string[] partNames;
    private readonly byte[][] partNameBytes;
    private readonly bool[] consumed;
    private readonly SpooledPart?[] spools;
    private readonly bool spooling;

    private int pendingPartIndex = PendingNone;
    private bool finished;
    private long remainingSpoolBudget;
    private long maxSpooledBodyLength;
    private int wireIndex = -1;
    private bool hasPendingMixedPart;

    private MultipartStreamingDriver(
        StreamingMultipartReader reader,
        PooledBufferWriter projection,
        byte[] boundaryBuffer,
        string[] partNames,
        bool spooling)
    {
        this.reader = reader;
        this.projection = projection;
        this.boundaryBuffer = boundaryBuffer;
        this.partNames = partNames;
        this.partNameBytes = new byte[partNames.Length][];
        for (int i = 0; i < partNames.Length; i++)
        {
            this.partNameBytes[i] = System.Text.Encoding.UTF8.GetBytes(partNames[i]);
        }

        this.consumed = new bool[partNames.Length];
        this.spools = new SpooledPart?[partNames.Length];
        this.spooling = spooling;
    }

    /// <summary>
    /// Gets the UTF-8 JSON projection of the body's non-binary parts. The memory is
    /// pooled and valid until the driver is disposed.
    /// </summary>
    public ReadOnlyMemory<byte> ProjectionUtf8Json => this.projection.WrittenMemory;

    /// <summary>
    /// Begins driving a streaming multipart body: reads the non-binary parts into the
    /// JSON projection. Under <see cref="MultipartBinaryOrdering.RequireBinaryLast"/>
    /// this stops at the first binary part; under
    /// <see cref="MultipartBinaryOrdering.SpoolOutOfOrder"/> the whole body is
    /// consumed, spooling the declared binary parts.
    /// </summary>
    /// <param name="body">The request body stream. The driver does not dispose it.</param>
    /// <param name="contentType">The request Content-Type header (carries the boundary).</param>
    /// <param name="binaryPartNames">The binary part names declared by the endpoint's schema.</param>
    /// <param name="options">The registration-time server options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The driver, positioned with the typed-body projection complete.</returns>
    /// <exception cref="RequestBodyTooLargeException">The non-binary parts exceeded <see cref="ApiServerOptions.MaxNonBinaryPartsLength"/>, or the spooled binary parts exceeded <see cref="ApiServerOptions.MaxSpooledBodyLength"/>.</exception>
    /// <exception cref="InvalidDataException">The body is not well-formed multipart content.</exception>
    public static async ValueTask<MultipartStreamingDriver> BeginAsync(
        Stream body,
        string? contentType,
        string[] binaryPartNames,
        ApiServerOptions options,
        CancellationToken cancellationToken = default)
    {
        // Encode the Content-Type and extract the boundary into a rented buffer that
        // outlives this method (the reader keeps only its own copy, but the extract
        // works over spans, so encode first).
        int byteCount = contentType is not null
            ? System.Text.Encoding.UTF8.GetByteCount(contentType)
            : 0;
        byte[] ctBuffer = FormFieldReader.Rent(Math.Max(byteCount, 1));
        StreamingMultipartReader reader;
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
        }
        catch
        {
            FormFieldReader.Return(ctBuffer);
            throw;
        }

        PooledBufferWriter? projection = null;
        MultipartStreamingDriver driver;
        try
        {
            projection = new PooledBufferWriter(4096);
            driver = new(reader, projection, ctBuffer, binaryPartNames, options.MultipartBinaryOrdering == MultipartBinaryOrdering.SpoolOutOfOrder);
        }
        catch
        {
            projection?.Dispose();
            await reader.DisposeAsync().ConfigureAwait(false);
            FormFieldReader.Return(ctBuffer);
            throw;
        }

        try
        {
            if (driver.spooling)
            {
                await driver.BuildProjectionAndSpoolAsync(options, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await driver.BuildProjectionAsync(options.MaxNonBinaryPartsLength, cancellationToken).ConfigureAwait(false);
            }

            return driver;
        }
        catch
        {
            await driver.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Begins driving a streaming <c>multipart/mixed</c> body: reads the non-binary
    /// parts into a JSON array projection (positions compact, matching the buffered
    /// deserializer), stopping at the first binary part. Mixed bodies are positional,
    /// so they always stream live in wire order; the ordering policy in
    /// <paramref name="options"/> does not apply.
    /// </summary>
    /// <param name="body">The request body stream. The driver does not dispose it.</param>
    /// <param name="contentType">The request Content-Type header (carries the boundary).</param>
    /// <param name="options">The registration-time server options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The driver, positioned with the typed-body projection complete.</returns>
    /// <exception cref="RequestBodyTooLargeException">The non-binary parts exceeded <see cref="ApiServerOptions.MaxNonBinaryPartsLength"/>.</exception>
    /// <exception cref="InvalidDataException">The body is not well-formed multipart content.</exception>
    public static async ValueTask<MultipartStreamingDriver> BeginMixedAsync(
        Stream body,
        string? contentType,
        ApiServerOptions options,
        CancellationToken cancellationToken = default)
    {
        int byteCount = contentType is not null
            ? System.Text.Encoding.UTF8.GetByteCount(contentType)
            : 0;
        byte[] ctBuffer = FormFieldReader.Rent(Math.Max(byteCount, 1));
        StreamingMultipartReader reader;
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
        }
        catch
        {
            FormFieldReader.Return(ctBuffer);
            throw;
        }

        PooledBufferWriter? projection = null;
        MultipartStreamingDriver driver;
        try
        {
            projection = new PooledBufferWriter(4096);
            driver = new(reader, projection, ctBuffer, [], spooling: false);
        }
        catch
        {
            projection?.Dispose();
            await reader.DisposeAsync().ConfigureAwait(false);
            FormFieldReader.Return(ctBuffer);
            throw;
        }

        try
        {
            await driver.BuildMixedProjectionAsync(options.MaxNonBinaryPartsLength, cancellationToken).ConfigureAwait(false);
            return driver;
        }
        catch
        {
            await driver.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Creates a handle for the named binary part.
    /// </summary>
    /// <param name="partName">The part's form field name; must be one of the declared binary part names.</param>
    /// <param name="required">Whether the part is required by the spec; opening a
    /// missing required part throws <see cref="RequiredBinaryPartMissingException"/>.</param>
    /// <returns>The handle.</returns>
    public BinaryPartHandle GetHandle(string partName, bool required) => new(this, partName, required);

    /// <summary>
    /// Creates a handle for the binary <c>multipart/mixed</c> part at the given wire
    /// index. Opening a missing part (the body ended before that index) yields
    /// <see langword="null"/>, matching the buffered path's empty binding.
    /// </summary>
    /// <param name="wireIndex">The part's zero-based position in the multipart message.</param>
    /// <returns>The handle.</returns>
    public BinaryPartHandle GetMixedHandle(int wireIndex) => new(this, wireIndex);

    /// <summary>
    /// Creates the sequence over the repeating binary items that follow the prefix
    /// parts of a <c>multipart/mixed</c> body.
    /// </summary>
    /// <param name="startIndex">The wire index at which the repeating items begin (the prefix part count).</param>
    /// <returns>The sequence.</returns>
    public BinaryPartSequence GetItemSequence(int startIndex) => new(this, startIndex);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (SpooledPart? spool in this.spools)
        {
            spool?.Dispose();
        }

        this.projection.Dispose();
        await this.reader.DisposeAsync().ConfigureAwait(false);
        FormFieldReader.Return(this.boundaryBuffer);
    }

    internal async ValueTask<Stream?> OpenPartAsync(string partName, bool required, CancellationToken cancellationToken)
    {
        int index = this.FindDeclaredIndex(partName);
        if (index < 0)
        {
            ThrowHelper.ThrowUnknownBinaryPart(partName);
        }

        if (this.consumed[index])
        {
            ThrowHelper.ThrowBinaryPartAlreadyPassed(partName);
        }

        if (this.spooling)
        {
            return this.OpenSpooledPart(index, partName, required);
        }

        while (true)
        {
            if (this.pendingPartIndex == PendingNone)
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

                this.pendingPartIndex = this.FindWireIndex(this.reader.CurrentName);
            }

            if (this.pendingPartIndex == index)
            {
                this.consumed[index] = true;
                this.pendingPartIndex = PendingNone;
                return this.reader.CurrentBodyStream;
            }

            // A different binary part is next on the wire: it is passed over
            // permanently (the next MoveNext drains it).
            if (this.pendingPartIndex >= 0)
            {
                this.consumed[this.pendingPartIndex] = true;
            }

            this.pendingPartIndex = PendingNone;
        }
    }

    internal async ValueTask<Stream?> OpenMixedPartAsync(int targetWireIndex, CancellationToken cancellationToken)
    {
        if (this.wireIndex > targetWireIndex || (this.wireIndex == targetWireIndex && !this.hasPendingMixedPart))
        {
            ThrowHelper.ThrowBinaryPartAlreadyPassed($"#{targetWireIndex}");
        }

        while (true)
        {
            if (!this.hasPendingMixedPart)
            {
                if (this.finished || !await this.reader.MoveNextPartAsync(cancellationToken).ConfigureAwait(false))
                {
                    // The body ended before this part: bind as absent, matching the
                    // buffered path's empty binding for missing mixed parts.
                    this.finished = true;
                    return null;
                }

                this.wireIndex++;
                if (!this.reader.CurrentIsBinary)
                {
                    // Mixed bodies stream in wire order, so every part after the
                    // typed body's non-binary prefix must be binary.
                    ThrowHelper.ThrowMultipartOrderingViolation();
                }

                this.hasPendingMixedPart = true;
            }

            if (this.wireIndex == targetWireIndex)
            {
                this.hasPendingMixedPart = false;
                return this.reader.CurrentBodyStream;
            }

            // A different part is next on the wire: it is passed over permanently
            // (the next MoveNext drains it).
            this.hasPendingMixedPart = false;
        }
    }

    internal async ValueTask<Stream?> OpenNextItemAsync(int startIndex, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (this.hasPendingMixedPart)
            {
                this.hasPendingMixedPart = false;
                if (this.wireIndex >= startIndex)
                {
                    return this.reader.CurrentBodyStream;
                }

                // A prefix part left unconsumed before the items: passed over
                // permanently (the next MoveNext drains it).
                continue;
            }

            if (this.finished || !await this.reader.MoveNextPartAsync(cancellationToken).ConfigureAwait(false))
            {
                this.finished = true;
                return null;
            }

            this.wireIndex++;
            if (!this.reader.CurrentIsBinary)
            {
                ThrowHelper.ThrowMultipartOrderingViolation();
            }

            this.hasPendingMixedPart = true;
        }
    }

    private static FileStream CreateSpoolFile(string? directory)
    {
        string path = Path.Combine(directory ?? Path.GetTempPath(), Path.GetRandomFileName());
        return new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
    }

    private Stream? OpenSpooledPart(int index, string partName, bool required)
    {
        SpooledPart? spool = this.spools[index];
        if (spool is null)
        {
            if (required)
            {
                ThrowHelper.ThrowRequiredBinaryPartMissing(partName);
            }

            return null;
        }

        this.consumed[index] = true;
        return spool.OpenStream();
    }

    private int FindDeclaredIndex(string partName)
    {
        for (int i = 0; i < this.partNames.Length; i++)
        {
            if (string.Equals(this.partNames[i], partName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private int FindWireIndex(ReadOnlySpan<byte> wireName)
    {
        for (int i = 0; i < this.partNameBytes.Length; i++)
        {
            if (wireName.SequenceEqual(this.partNameBytes[i]))
            {
                return i;
            }
        }

        return PendingUndeclared;
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
                    this.pendingPartIndex = this.FindWireIndex(this.reader.CurrentName);
                    break;
                }

                scratch ??= FormFieldReader.Rent(4096);
                (budget, scratch) = await this.ProjectTextPartAsync(writer, budget, maxNonBinaryBytes, scratch, cancellationToken).ConfigureAwait(false);
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

    private async ValueTask BuildProjectionAndSpoolAsync(ApiServerOptions options, CancellationToken cancellationToken)
    {
        using Utf8JsonWriter writer = new(this.projection);
        writer.WriteStartObject();

        long budget = options.MaxNonBinaryPartsLength;
        this.remainingSpoolBudget = options.MaxSpooledBodyLength;
        this.maxSpooledBodyLength = options.MaxSpooledBodyLength;
        byte[]? scratch = null;
        try
        {
            while (await this.reader.MoveNextPartAsync(cancellationToken).ConfigureAwait(false))
            {
                if (this.reader.CurrentIsBinary)
                {
                    int index = this.FindWireIndex(this.reader.CurrentName);
                    if (index < 0 || this.spools[index] is not null)
                    {
                        // Undeclared or duplicate binary part: the next MoveNext drains it.
                        continue;
                    }

                    this.spools[index] = await this.SpoolPartAsync(options.SpoolMemoryThresholdBytes, options.SpoolDirectory, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                scratch ??= FormFieldReader.Rent(4096);
                (budget, scratch) = await this.ProjectTextPartAsync(writer, budget, options.MaxNonBinaryPartsLength, scratch, cancellationToken).ConfigureAwait(false);
            }

            this.finished = true;
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

    /// <summary>
    /// Projects the reader's current non-binary part into the JSON writer, bounded by
    /// the remaining budget. Returns the remaining budget and the (possibly regrown)
    /// scratch buffer, which the caller owns.
    /// </summary>
    private async ValueTask<(long Budget, byte[] Scratch)> ProjectTextPartAsync(Utf8JsonWriter writer, long budget, long maxNonBinaryBytes, byte[] scratch, CancellationToken cancellationToken)
    {
        if (this.reader.CurrentName.IsEmpty)
        {
            // Unnamed non-binary part: nothing to project it as; skip it.
            return (budget, scratch);
        }

        int length;
        (length, budget, scratch) = await this.ReadCurrentPartAsync(budget, maxNonBinaryBytes, scratch, cancellationToken).ConfigureAwait(false);

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

        return (budget, scratch);
    }

    private async ValueTask BuildMixedProjectionAsync(long maxNonBinaryBytes, CancellationToken cancellationToken)
    {
        using Utf8JsonWriter writer = new(this.projection);
        writer.WriteStartArray();

        long budget = maxNonBinaryBytes;
        byte[]? scratch = null;
        try
        {
            while (await this.reader.MoveNextPartAsync(cancellationToken).ConfigureAwait(false))
            {
                this.wireIndex++;
                if (this.reader.CurrentIsBinary)
                {
                    // The typed body is complete; the binary tail is consumed via
                    // handles and the item sequence.
                    this.hasPendingMixedPart = true;
                    break;
                }

                int length;
                scratch ??= FormFieldReader.Rent(4096);
                (length, budget, scratch) = await this.ReadCurrentPartAsync(budget, maxNonBinaryBytes, scratch, cancellationToken).ConfigureAwait(false);

                // Value semantics match MultipartMixedReader.DeserializeToJson: JSON
                // or untyped parts project raw, other text parts as JSON strings.
                ReadOnlySpan<byte> value = scratch.AsSpan(0, length);
                if (MultipartFormReader.IsJsonContentType(this.reader.CurrentContentType) || this.reader.CurrentContentType.IsEmpty)
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
                    writer.WriteStringValue(value);
                }
            }

            writer.WriteEndArray();
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

    /// <summary>
    /// Reads the reader's current part body into the scratch buffer, growing it as
    /// needed and charging the non-binary budget. Returns the part length, the
    /// remaining budget, and the (possibly regrown) scratch buffer, which the caller
    /// owns.
    /// </summary>
    private async ValueTask<(int Length, long Budget, byte[] Scratch)> ReadCurrentPartAsync(long budget, long maxNonBinaryBytes, byte[] scratch, CancellationToken cancellationToken)
    {
        int length = 0;
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
                return (length, budget, scratch);
            }

            length += read;
            budget -= read;
            if (budget < 0)
            {
                ThrowHelper.ThrowRequestBodyTooLarge(maxNonBinaryBytes);
            }
        }
    }

    /// <summary>
    /// Spools the reader's current binary part: pooled memory while the part stays at
    /// or below the threshold, migrating to a delete-on-close temporary file when it
    /// grows past it.
    /// </summary>
    private async ValueTask<SpooledPart> SpoolPartAsync(int memoryThreshold, string? directory, CancellationToken cancellationToken)
    {
        Stream partBody = this.reader.CurrentBodyStream;
        byte[] buffer = FormFieldReader.Rent(Math.Min(4096, Math.Max(memoryThreshold, 1)));
        FileStream? file = null;
        int length = 0;
        try
        {
            while (true)
            {
                if (file is null)
                {
                    if (length == buffer.Length)
                    {
                        byte[] larger = FormFieldReader.Rent(buffer.Length * 2);
                        buffer.AsSpan(0, length).CopyTo(larger);
                        FormFieldReader.Return(buffer);
                        buffer = larger;
                    }

                    int read = await partBody.ReadAsync(buffer.AsMemory(length, buffer.Length - length), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        SpooledPart memorySpool = new(buffer, length);
                        buffer = [];
                        return memorySpool;
                    }

                    length += read;
                    this.TakeSpoolBudget(read);
                    if (length > memoryThreshold)
                    {
                        file = CreateSpoolFile(directory);
                        await file.WriteAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    int read = await partBody.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        SpooledPart fileSpool = new(file);
                        file = null;
                        return fileSpool;
                    }

                    this.TakeSpoolBudget(read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (buffer.Length > 0)
            {
                FormFieldReader.Return(buffer);
            }

            file?.Dispose();
        }
    }

    private void TakeSpoolBudget(int read)
    {
        this.remainingSpoolBudget -= read;
        if (this.remainingSpoolBudget < 0)
        {
            ThrowHelper.ThrowRequestBodyTooLarge(this.maxSpooledBodyLength);
        }
    }

    /// <summary>
    /// One spooled binary part: either pooled memory or a delete-on-close temporary
    /// file. Disposal returns the pooled buffer or deletes the file.
    /// </summary>
    private sealed class SpooledPart : IDisposable
    {
        private readonly int length;
        private byte[]? buffer;
        private FileStream? file;

        public SpooledPart(byte[] buffer, int length)
        {
            this.buffer = buffer;
            this.length = length;
        }

        public SpooledPart(FileStream file)
        {
            this.file = file;
        }

        public Stream OpenStream()
        {
            if (this.file is { } f)
            {
                f.Position = 0;
                return f;
            }

            return new MemoryStream(this.buffer ?? [], 0, this.length, writable: false);
        }

        public void Dispose()
        {
            if (this.buffer is { Length: > 0 } b)
            {
                FormFieldReader.Return(b);
            }

            this.buffer = null;
            this.file?.Dispose();
            this.file = null;
        }
    }
}