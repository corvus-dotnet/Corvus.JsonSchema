// <copyright file="LoopbackServerlessInvokeAuthenticator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The <see cref="IServerlessInvokeAuthenticator"/> for a function host that shares the runner's machine, such as the
/// micro-guest sidecar (ADR 0063) or a local Functions host. It adds no credential, and it refuses to let an invocation
/// leave for any address that is not on the loopback interface, so it cannot stand in for a platform's authenticator by
/// mistake.
/// </summary>
public sealed class LoopbackServerlessInvokeAuthenticator : IServerlessInvokeAuthenticator
{
    private LoopbackServerlessInvokeAuthenticator()
    {
    }

    /// <summary>Gets the shared instance.</summary>
    public static LoopbackServerlessInvokeAuthenticator Instance { get; } = new();

    /// <inheritdoc/>
    public ValueTask AuthenticateAsync(HttpRequestMessage request, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestUri is not { IsAbsoluteUri: true, IsLoopback: true })
        {
            ThrowHelper.ThrowServerlessInvokeNotLoopback(request.RequestUri);
        }

        return ValueTask.CompletedTask;
    }
}