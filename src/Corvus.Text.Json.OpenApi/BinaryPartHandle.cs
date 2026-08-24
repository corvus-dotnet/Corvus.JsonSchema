// <copyright file="BinaryPartHandle.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A handle to one binary part of a streaming multipart request, carried on a
/// generated Params struct. Opening the handle returns the part's content as a
/// stream; each handle can be opened at most once.
/// </summary>
/// <remarks>
/// <para>
/// Under <see cref="MultipartBinaryOrdering.RequireBinaryLast"/> the stream reads
/// live off the wire and parts are consumed in wire order: opening a handle drains
/// any earlier unconsumed binary parts permanently, and opening a part that has
/// already been passed throws. Under
/// <see cref="MultipartBinaryOrdering.SpoolOutOfOrder"/> the stream reads from the
/// part's spool and handles may be opened in any order.
/// </para>
/// <para>
/// The stream is valid only for the duration of the handler call (and, under
/// RequireBinaryLast, only until another handle is opened); consume it before the
/// handler returns.
/// </para>
/// </remarks>
public readonly struct BinaryPartHandle
{
    private readonly MultipartStreamingDriver? driver;
    private readonly string? partName;
    private readonly bool required;
    private readonly int mixedWireIndex;

    internal BinaryPartHandle(MultipartStreamingDriver driver, string partName, bool required)
    {
        this.driver = driver;
        this.partName = partName;
        this.required = required;
        this.mixedWireIndex = -1;
    }

    internal BinaryPartHandle(MultipartStreamingDriver driver, int mixedWireIndex)
    {
        this.driver = driver;
        this.partName = null;
        this.required = false;
        this.mixedWireIndex = mixedWireIndex;
    }

    /// <summary>
    /// Gets the part's form field name, or <see langword="null"/> for a positional
    /// <c>multipart/mixed</c> part.
    /// </summary>
    public string? PartName => this.partName;

    /// <summary>
    /// Opens the part's content stream, advancing the wire to this part.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// The part's forward-only content stream, or <see langword="null"/> when an
    /// optional part is absent from the body.
    /// </returns>
    /// <exception cref="RequiredBinaryPartMissingException">The part is required and absent.</exception>
    /// <exception cref="MultipartOrderingException">A non-binary part arrived after a binary part under RequireBinaryLast.</exception>
    /// <exception cref="InvalidOperationException">The part has already been passed in wire order.</exception>
    public ValueTask<Stream?> OpenStreamAsync(CancellationToken cancellationToken = default)
    {
        if (this.driver is not { } d)
        {
            return ValueTask.FromResult<Stream?>(null);
        }

        return this.partName is { } name
            ? d.OpenPartAsync(name, this.required, cancellationToken)
            : d.OpenMixedPartAsync(this.mixedWireIndex, cancellationToken);
    }
}