// <copyright file="WebSocketClose.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.WebSockets;

namespace Corvus.Text.Json.AsyncApi.WebSocket.Internal;

/// <summary>
/// Close helpers for <see cref="WebSocketMessageTransport"/>. Exposed through the
/// library's public <c>Internal</c> seam so the behaviour can be unit tested, which
/// is this codebase's alternative to <c>InternalsVisibleTo</c>.
/// </summary>
public static class WebSocketClose
{
    /// <summary>
    /// Closes a WebSocket on a best-effort basis for disposal. Sends a close frame
    /// without waiting for the peer's reply (<c>CloseOutputAsync</c>, not
    /// <c>CloseAsync</c>, which blocks on the full handshake), bounds it with a short
    /// timeout, and swallows the exceptions a torn-down connection raises. A dispose
    /// must not throw or hang because the peer already went away.
    /// </summary>
    /// <param name="webSocket">The socket to close.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the close has been attempted.</returns>
    public static async ValueTask CloseBestEffortAsync(System.Net.WebSockets.WebSocket webSocket, CancellationToken cancellationToken = default)
    {
        if (webSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            // Nothing to close (already closed, aborted, or never opened), and
            // CloseOutputAsync would itself throw for those states.
            return;
        }

        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            await webSocket.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                "Disposing",
                timeoutCts.Token).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
            // The peer closed the connection without completing the handshake.
        }
        catch (ObjectDisposedException)
        {
            // The underlying connection was already torn down.
        }
        catch (OperationCanceledException)
        {
            // The close frame did not flush within the timeout.
        }
    }
}