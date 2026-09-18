// <copyright file="IBoundableApiTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// An <see cref="IApiTransport"/> that can produce a version of itself which bounds each request in time and each
/// response in size.
/// </summary>
/// <remarks>
/// <para>
/// A host that must bound what it sends, whoever built the transport, asks through
/// <see cref="ApiTransportBounds.Apply"/>. A transport that reads a response body from the network implements this
/// interface, because only it sees the body as it arrives. A decorator implements it by bounding the transport it
/// wraps and wrapping the result again, so the bounds reach the transport that does the read.
/// </para>
/// <para>
/// The returned transport replaces this one. It owns whatever this transport owned, so the caller disposes the
/// returned transport and not this one.
/// </para>
/// </remarks>
public interface IBoundableApiTransport : IApiTransport
{
    /// <summary>
    /// Gets a transport equivalent to this one that applies the given bounds.
    /// </summary>
    /// <param name="requestTimeout">The longest a single request may take, from the send to the response having been
    /// produced from its body, or <see langword="null"/> for no bound. A request that runs past it fails with an
    /// <see cref="ApiTransportTimeoutException"/>.</param>
    /// <param name="maxResponseLength">The largest response body, in bytes, the transport will read, or
    /// <see langword="null"/> for no bound. A larger response fails with an <see cref="ApiResponseTooLargeException"/>.</param>
    /// <returns>The bounded transport, which replaces this one.</returns>
    IApiTransport WithBounds(TimeSpan? requestTimeout, long? maxResponseLength);
}