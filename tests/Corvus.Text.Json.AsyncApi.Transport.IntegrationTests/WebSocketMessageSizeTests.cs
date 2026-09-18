// <copyright file="WebSocketMessageSizeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.WebSockets;
using System.Text;
using Corvus.Text.Json.AsyncApi.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WS = System.Net.WebSockets.WebSocket;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// <see cref="WebSocketTransportOptions.MaxMessageSize"/>: the transport buffers a whole message before dispatching
/// it, so with no limit a peer decides how much memory the client spends. Each test plays the peer itself, frame by
/// frame, over a real socket to an in-process server, because the defect is in how frames accumulate.
/// </summary>
[TestClass]
public class WebSocketMessageSizeTests
{
    private const int TestTimeout = 30_000;

    [TestMethod]
    [Timeout(TestTimeout)]
    public async Task A_message_that_outgrows_the_limit_across_frames_is_refused_without_being_read_to_its_end()
    {
        // The defect shape: no single frame is over the limit, and the message has no end.
        int framesSent = 0;
        await using ScriptedPeer peer = await ScriptedPeer.StartAsync(async ws =>
        {
            byte[] frame = Encoding.UTF8.GetBytes(new string('x', 60));
            try
            {
                for (; framesSent < 200; framesSent++)
                {
                    await ws.SendAsync(frame, WebSocketMessageType.Text, endOfMessage: false, CancellationToken.None);
                }
            }
            catch (WebSocketException)
            {
                // The client stopped taking the message, which is the point.
            }
        });

        await using WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = peer.ServerUri,
            MaxMessageSize = 100,
            ReceiveBufferSize = 64,
        });

        Assert.AreEqual(WebSocketCloseStatus.MessageTooBig, await peer.CloseStatus);
    }

    [TestMethod]
    [Timeout(TestTimeout)]
    public async Task A_message_one_byte_over_the_limit_is_refused_and_never_dispatched()
    {
        byte[] envelope = Encoding.UTF8.GetBytes("""{"channel":"ws/size/over","type":"publish","payload":{"n":1}}""");
        bool dispatched = false;
        await using ScriptedPeer peer = await ScriptedPeer.StartAsync(async ws =>
        {
            await ReceiveOneAsync(ws); // the subscribe envelope
            await ws.SendAsync(envelope, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
        });

        await using WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = peer.ServerUri,
            MaxMessageSize = envelope.Length - 1,
        });
        await transport.SubscribeAsync<JsonElement>("ws/size/over"u8.ToArray(), (_, _, _) =>
        {
            dispatched = true;
            return ValueTask.CompletedTask;
        });

        Assert.AreEqual(WebSocketCloseStatus.MessageTooBig, await peer.CloseStatus);
        Assert.IsFalse(dispatched);
    }

    [TestMethod]
    [Timeout(TestTimeout)]
    public async Task A_message_exactly_at_the_limit_is_dispatched()
    {
        byte[] envelope = Encoding.UTF8.GetBytes("""{"channel":"ws/size/at","type":"publish","payload":{"n":1}}""");
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using ScriptedPeer peer = await ScriptedPeer.StartAsync(async ws =>
        {
            await ReceiveOneAsync(ws);

            // In two frames, so the count that is compared with the limit is the sum.
            await ws.SendAsync(envelope.AsMemory(0, 10), WebSocketMessageType.Text, endOfMessage: false, CancellationToken.None);
            await ws.SendAsync(envelope.AsMemory(10), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
            await ReceiveOneAsync(ws); // hold the connection open until the client goes
        });

        await using WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = peer.ServerUri,
            MaxMessageSize = envelope.Length,
        });
        await transport.SubscribeAsync<JsonElement>("ws/size/at"u8.ToArray(), (payload, _, _) =>
        {
            received.TrySetResult(payload.GetProperty("n"u8).GetInt32());
            return ValueTask.CompletedTask;
        });

        Assert.AreEqual(1, await received.Task);
    }

    [TestMethod]
    [Timeout(TestTimeout)]
    public async Task With_no_limit_set_a_large_message_is_dispatched_as_before()
    {
        string big = new('y', 200_000);
        byte[] envelope = Encoding.UTF8.GetBytes("{\"channel\":\"ws/size/unset\",\"type\":\"publish\",\"payload\":{\"s\":\"" + big + "\"}}");
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using ScriptedPeer peer = await ScriptedPeer.StartAsync(async ws =>
        {
            await ReceiveOneAsync(ws);
            await ws.SendAsync(envelope, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
            await ReceiveOneAsync(ws);
        });

        await using WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions { ServerUri = peer.ServerUri });
        await transport.SubscribeAsync<JsonElement>("ws/size/unset"u8.ToArray(), (payload, _, _) =>
        {
            received.TrySetResult(payload.GetProperty("s"u8).GetString()!.Length);
            return ValueTask.CompletedTask;
        });

        Assert.AreEqual(200_000, await received.Task);
    }

    [TestMethod]
    [Timeout(TestTimeout)]
    public async Task A_request_awaiting_its_reply_is_failed_with_the_reason_and_not_left_to_time_out()
    {
        // The reply could only have come over the connection that was just given up.
        await using ScriptedPeer peer = await ScriptedPeer.StartAsync(async ws =>
        {
            await ReceiveOneAsync(ws); // the request
            await ws.SendAsync(new byte[500], WebSocketMessageType.Binary, endOfMessage: false, CancellationToken.None);
        });

        await using WebSocketMessageTransport transport = await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions
        {
            ServerUri = peer.ServerUri,
            MaxMessageSize = 256,
        });
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        using ParsedJsonDocument<JsonElement> request = ParsedJsonDocument<JsonElement>.Parse("""{"q":1}"""u8.ToArray());

        // No cancellation and no timeout of its own: only the transport can end this wait.
        WebSocketMessageTooLargeException thrown = await Assert.ThrowsExactlyAsync<WebSocketMessageTooLargeException>(async () =>
            await transport.RequestAsync<JsonElement, JsonElement>(
                "ws/size/request"u8.ToArray(),
                "ws/size/reply"u8.ToArray(),
                request.RootElement,
                "corr-1"u8.ToArray(),
                workspace));

        Assert.AreEqual(256, thrown.MaxMessageSize);
        Assert.AreEqual(WebSocketCloseStatus.MessageTooBig, await peer.CloseStatus);
    }

    [TestMethod]
    public async Task A_limit_that_admits_nothing_is_rejected_before_connecting()
    {
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () =>
            await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions { ServerUri = "ws://127.0.0.1:1/never", MaxMessageSize = 0 }));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () =>
            await WebSocketMessageTransport.CreateAsync(new WebSocketTransportOptions { ServerUri = "ws://127.0.0.1:1/never", MaxMessageSize = -1 }));
    }

    private static async Task<WebSocketReceiveResult> ReceiveOneAsync(WS ws)
    {
        byte[] buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }
        while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);

        return result;
    }

    // A WebSocket server that runs one script against the one connection it accepts, then reads until the client's
    // close frame and reports the status it carried.
    private sealed class ScriptedPeer : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly TaskCompletionSource<WebSocketCloseStatus?> closeStatus = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private ScriptedPeer(WebApplication app) => this.app = app;

        public string ServerUri { get; private set; } = string.Empty;

        public Task<WebSocketCloseStatus?> CloseStatus => this.closeStatus.Task;

        public static async Task<ScriptedPeer> StartAsync(Func<WS, Task> script)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            WebApplication app = builder.Build();
            var peer = new ScriptedPeer(app);
            app.UseWebSockets();
            app.Map("/ws", async context =>
            {
                WS ws = await context.WebSockets.AcceptWebSocketAsync();
                try
                {
                    await script(ws);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ReceiveOneAsync(ws);
                    }
                    while (result.MessageType != WebSocketMessageType.Close);

                    peer.closeStatus.TrySetResult(result.CloseStatus);
                }
                catch (Exception ex)
                {
                    peer.closeStatus.TrySetException(ex);
                }
            });
            await app.StartAsync();
            string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            peer.ServerUri = address.Replace("http://", "ws://") + "/ws";
            return peer;
        }

        public async ValueTask DisposeAsync()
        {
            await this.app.StopAsync();
            await this.app.DisposeAsync();
        }
    }
}