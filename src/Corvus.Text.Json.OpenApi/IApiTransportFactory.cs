// <copyright file="IApiTransportFactory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Creates an <see cref="IApiTransport"/> for a single use. A caller that owns the per-use transport
/// lifetime (for example a workflow runner that disposes the transport after each run) takes a factory
/// rather than a shared instance, so each use gets a fresh transport over the host's shared, long-lived
/// resources (such as an <see cref="System.Net.Http.HttpClient"/>).
/// </summary>
public interface IApiTransportFactory
{
    /// <summary>Creates a new <see cref="IApiTransport"/>. The caller owns and disposes it.</summary>
    /// <returns>The new transport.</returns>
    IApiTransport CreateTransport();
}