// <copyright file="BinaryPartSequence.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A forward-only sequence over the repeating binary items that follow the prefix
/// parts of a streaming <c>multipart/mixed</c> request, carried on a generated
/// Params struct.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="MoveNextAsync"/> to open each item's content stream in wire
/// order; it returns <see langword="null"/> when the body has no more items. Moving
/// to the next item drains any unread remainder of the previous one, and consuming
/// the sequence passes over any binary prefix parts whose handles were not opened
/// first. Each stream is valid only until the next call and only for the duration
/// of the handler call.
/// </para>
/// </remarks>
public readonly struct BinaryPartSequence
{
    private readonly MultipartStreamingDriver? driver;
    private readonly int startIndex;

    internal BinaryPartSequence(MultipartStreamingDriver driver, int startIndex)
    {
        this.driver = driver;
        this.startIndex = startIndex;
    }

    /// <summary>
    /// Opens the next repeating item's content stream, advancing the wire past any
    /// unconsumed earlier parts.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// The item's forward-only content stream, or <see langword="null"/> when the
    /// body has no more items.
    /// </returns>
    /// <exception cref="MultipartOrderingException">A non-binary part was encountered among the repeating items.</exception>
    public ValueTask<Stream?> MoveNextAsync(CancellationToken cancellationToken = default)
        => this.driver is { } d
            ? d.OpenNextItemAsync(this.startIndex, cancellationToken)
            : ValueTask.FromResult<Stream?>(null);
}