// <copyright file="WebSocketCloseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.WebSockets;
using Corvus.Text.Json.AsyncApi.WebSocket.Internal;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Unit tests for the best-effort WebSocket close used by
/// <see cref="Corvus.Text.Json.AsyncApi.WebSocket.WebSocketMessageTransport"/> disposal.
/// These use a fake socket and need no server, so they run outside the integration
/// category. Regression for the teardown race where a peer resets the connection
/// during the close handshake.
/// </summary>
[TestClass]
public class WebSocketCloseTests
{
    [TestMethod]
    public async Task CloseBestEffort_WhenCloseThrowsWebSocketException_DoesNotPropagate()
    {
        // The exact failure that flaked in CI: the peer reset the connection, so the
        // close frame's send throws WebSocketException. Disposal must swallow it.
        FakeWebSocket socket = new(
            WebSocketState.Open,
            onCloseOutput: () => throw new WebSocketException(
                "The remote party closed the WebSocket connection without completing the close handshake."));

        await WebSocketClose.CloseBestEffortAsync(socket);

        Assert.IsTrue(socket.CloseOutputCalled, "a close frame should have been attempted");
    }

    [TestMethod]
    public async Task CloseBestEffort_WhenCloseThrowsObjectDisposed_DoesNotPropagate()
    {
        FakeWebSocket socket = new(
            WebSocketState.Open,
            onCloseOutput: () => throw new ObjectDisposedException("System.Net.Sockets.NetworkStream"));

        await WebSocketClose.CloseBestEffortAsync(socket);

        Assert.IsTrue(socket.CloseOutputCalled);
    }

    [TestMethod]
    public async Task CloseBestEffort_WhenOpen_SendsCloseFrame()
    {
        FakeWebSocket socket = new(WebSocketState.Open, onCloseOutput: () => { });

        await WebSocketClose.CloseBestEffortAsync(socket);

        Assert.IsTrue(socket.CloseOutputCalled);
    }

    [TestMethod]
    public async Task CloseBestEffort_WhenNotOpen_DoesNotCloseOrThrow()
    {
        // A socket the peer already aborted must be left alone: CloseOutputAsync would
        // itself throw for a non-open state.
        FakeWebSocket socket = new(
            WebSocketState.Aborted,
            onCloseOutput: () => throw new InvalidOperationException("close must not be attempted on an aborted socket"));

        await WebSocketClose.CloseBestEffortAsync(socket);

        Assert.IsFalse(socket.CloseOutputCalled);
    }

    private sealed class FakeWebSocket : System.Net.WebSockets.WebSocket
    {
        private readonly Action onCloseOutput;

        public FakeWebSocket(WebSocketState state, Action onCloseOutput)
        {
            this.State = state;
            this.onCloseOutput = onCloseOutput;
        }

        public bool CloseOutputCalled { get; private set; }

        public override WebSocketCloseStatus? CloseStatus => null;

        public override string? CloseStatusDescription => null;

        public override WebSocketState State { get; }

        public override string? SubProtocol => null;

        public override void Abort()
        {
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            => throw new NotSupportedException("A best-effort close must not use the full-handshake CloseAsync.");

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            this.CloseOutputCalled = true;
            this.onCloseOutput();
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}