// <copyright file="BinaryPartHandle.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A handle to one binary part of a streaming multipart request, carried on a
/// generated Params struct. Opening the handle advances the wire to that part and
/// returns its content as a forward-only stream that is never buffered.
/// </summary>
/// <remarks>
/// <para>
/// Binary parts are consumed in wire order: opening a handle drains any earlier
/// unconsumed binary parts permanently, and opening a part that has already been
/// passed throws. The stream is valid until another handle is opened or the request
/// completes; consume it before the handler returns.
/// </para>
/// </remarks>
public readonly struct BinaryPartHandle
{
    private readonly MultipartStreamingDriver? driver;
    private readonly string? partName;
    private readonly bool required;

    internal BinaryPartHandle(MultipartStreamingDriver driver, string partName, bool required)
    {
        this.driver = driver;
        this.partName = partName;
        this.required = required;
    }

    /// <summary>
    /// Gets the part's form field name.
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
        => this.driver is { } d && this.partName is { } name
            ? d.OpenPartAsync(name, this.required, cancellationToken)
            : ValueTask.FromResult<Stream?>(null);
}