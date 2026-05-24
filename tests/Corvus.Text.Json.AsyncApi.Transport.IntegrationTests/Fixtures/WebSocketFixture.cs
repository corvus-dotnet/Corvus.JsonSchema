// <copyright file="WebSocketFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STJ = System.Text.Json;
using WS = System.Net.WebSockets.WebSocket;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

/// <summary>
/// Hosts an in-process WebSocket relay server for integration tests.
/// Incoming envelopes with a "channel" field are relayed to all other
/// connected clients that have subscribed to that channel.
/// </summary>
internal static class WebSocketFixture
{
    private static WebApplication? s_app;
    private static readonly ConcurrentDictionary<string, ConcurrentBag<WS>> Subscriptions = new(StringComparer.Ordinal);
    private static readonly ConcurrentBag<WS> Connections = [];

    /// <summary>
    /// Gets the server URI (e.g., "ws://localhost:PORT/ws").
    /// </summary>
    public static string ServerUri { get; private set; } = string.Empty;

    /// <summary>
    /// Starts the in-process WebSocket relay server on a random port.
    /// </summary>
    /// <returns>A task that completes when the server is ready to accept connections.</returns>
    public static async Task StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            Args = [],
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        s_app = builder.Build();
        s_app.UseWebSockets();
        s_app.Map("/ws", HandleWebSocket);

        await s_app.StartAsync().ConfigureAwait(false);

        // Get the actual bound address via IServerAddressesFeature
        Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature? addressFeature =
            s_app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
                .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();

        string address = addressFeature?.Addresses.First() ?? s_app.Urls.First();
        ServerUri = address.Replace("http://", "ws://") + "/ws";
    }

    /// <summary>
    /// Stops the relay server.
    /// </summary>
    /// <returns>A task that completes when the server has shut down.</returns>
    public static async Task StopAsync()
    {
        if (s_app is not null)
        {
            await s_app.StopAsync().ConfigureAwait(false);
            await s_app.DisposeAsync().ConfigureAwait(false);
            s_app = null;
        }

        Subscriptions.Clear();
        Connections.Clear();
    }

    private static async Task HandleWebSocket(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        WS ws = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        Connections.Add(ws);

        byte[] buffer = new byte[16384];

        try
        {
            while (ws.State == WebSocketState.Open)
            {
                using MemoryStream ms = new();
                WebSocketReceiveResult result;

                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None).ConfigureAwait(false);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                byte[] messageBytes = ms.ToArray();
                await ProcessEnvelopeAsync(ws, messageBytes).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
            // Client disconnected
        }
    }

    private static async Task ProcessEnvelopeAsync(WS sender, byte[] envelopeBytes)
    {
        // Parse the envelope to extract channel and action
        using STJ.JsonDocument doc = STJ.JsonDocument.Parse(envelopeBytes);
        STJ.JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("channel", out STJ.JsonElement channelProp))
        {
            return;
        }

        string? channel = channelProp.GetString();
        if (channel is null)
        {
            return;
        }

        // Check for control envelopes (transport sends "type" field)
        if (root.TryGetProperty("type", out STJ.JsonElement typeProp))
        {
            string? type = typeProp.GetString();
            if (type == "subscribe")
            {
                ConcurrentBag<WS> bag = Subscriptions.GetOrAdd(channel, _ => []);
                bag.Add(sender);
                return;
            }

            if (type == "unsubscribe")
            {
                // Can't remove from ConcurrentBag easily; in practice this is fine for tests
                return;
            }
        }

        // It's a data envelope — relay to all subscribers of this channel
        if (Subscriptions.TryGetValue(channel, out ConcurrentBag<WS>? subscribers))
        {
            foreach (WS sub in subscribers)
            {
                if (sub != sender && sub.State == WebSocketState.Open)
                {
                    await sub.SendAsync(
                        new ArraySegment<byte>(envelopeBytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
    }
}