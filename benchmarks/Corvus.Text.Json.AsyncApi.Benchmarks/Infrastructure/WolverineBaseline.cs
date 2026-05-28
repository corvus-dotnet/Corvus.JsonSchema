// <copyright file="WolverineBaseline.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace AsyncApiBenchmark.Infrastructure;

/// <summary>
/// Wolverine message bus baseline — measures real framework dispatch overhead
/// (handler chain compilation, context pooling, invoker resolution).
/// </summary>
/// <remarks>
/// <para>
/// This is the comparison point for all benchmarks: what does a popular .NET
/// message bus framework cost vs the Corvus generated pipeline?
/// </para>
/// <para>
/// Subscribe: STJ deserialization + Wolverine dispatch + handler property access.
/// Publish: Wolverine dispatch through handler chain.
/// Request/Reply: Wolverine dispatch with typed response.
/// </para>
/// </remarks>
public sealed class WolverineBaseline : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Gets the serializer options used by the Wolverine baseline (STJ defaults with WhenWritingNull).
    /// </summary>
    public static JsonSerializerOptions SerializerOptions => Options;

    private IHost host = null!;
    private IMessageBus bus = null!;

    /// <summary>
    /// Configures Wolverine with handlers and starts the host.
    /// Call once during GlobalSetup.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public async Task StartAsync()
    {
        this.host = Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                opts.ApplicationAssembly = typeof(LightMeasuredWolverineHandler).Assembly;
                opts.Discovery.IncludeType<LightMeasuredWolverineHandler>();
                opts.Discovery.IncludeType<LightMeasuredRequestReplyHandler>();
                opts.Durability.Mode = DurabilityMode.MediatorOnly;
            })
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.ClearProviders());
            })
            .Build();

        await this.host.StartAsync().ConfigureAwait(false);
        this.bus = this.host.Services.GetRequiredService<IMessageBus>();
    }

    /// <summary>
    /// Deserializes bytes to a POCO using STJ (what Wolverine does internally on receive).
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="payloadJson">The raw JSON bytes from the transport.</param>
    /// <returns>The deserialized message.</returns>
    public static T Deserialize<T>(ReadOnlySpan<byte> payloadJson)
    {
        return JsonSerializer.Deserialize<T>(payloadJson, Options)!;
    }

    /// <summary>
    /// Dispatches a message through Wolverine's handler pipeline (fire-and-forget).
    /// </summary>
    /// <param name="message">The deserialized POCO message.</param>
    /// <returns>A task representing the async operation.</returns>
    public Task InvokeAsync(LightMeasuredPoco message)
    {
        return this.bus.InvokeAsync(message);
    }

    /// <summary>
    /// Dispatches a request and returns a typed response through Wolverine's pipeline.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="message">The request message.</param>
    /// <param name="options">Delivery options (correlation headers, tenant ID, etc.).</param>
    /// <returns>The response from the handler.</returns>
    public Task<TResponse> InvokeAsync<TResponse>(LightMeasuredRequestPoco message, DeliveryOptions options)
    {
        return this.bus.InvokeAsync<TResponse>(message, options);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.host is not null)
        {
            await this.host.StopAsync().ConfigureAwait(false);
            this.host.Dispose();
        }
    }
}

/// <summary>
/// Wolverine handler for the LightMeasured message.
/// Accesses properties to simulate real handler work.
/// </summary>
public class LightMeasuredWolverineHandler
{
    /// <summary>
    /// Handles a light measurement message by accessing its properties.
    /// </summary>
    /// <param name="message">The incoming message.</param>
    public static void Handle(LightMeasuredPoco message)
    {
        _ = message.Id;
        _ = message.Lumens;
        _ = message.SentAt;
    }
}

/// <summary>
/// Wolverine request/reply handler — receives a request and returns a reply.
/// </summary>
public class LightMeasuredRequestReplyHandler
{
    /// <summary>
    /// Handles a request and returns a reply POCO (simulates request/reply pattern).
    /// </summary>
    /// <param name="message">The incoming request.</param>
    /// <returns>The reply message.</returns>
    public static LightMeasuredPoco Handle(LightMeasuredRequestPoco message)
    {
        _ = message.Id;
        _ = message.Lumens;
        return new LightMeasuredPoco
        {
            Id = message.Id,
            Lumens = message.Lumens,
            SentAt = message.SentAt,
        };
    }
}

/// <summary>
/// A POCO representation of the light measured payload for Wolverine baseline comparison.
/// </summary>
public sealed class LightMeasuredPoco
{
    /// <summary>
    /// Gets or sets the sensor ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the lumens measurement.
    /// </summary>
    [JsonPropertyName("lumens")]
    public double Lumens { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    [JsonPropertyName("sentAt")]
    public string SentAt { get; set; } = string.Empty;
}

/// <summary>
/// A distinct request POCO for Wolverine request/reply routing.
/// Wolverine resolves handlers by message type, so request and fire-and-forget
/// messages need different CLR types.
/// </summary>
public sealed class LightMeasuredRequestPoco
{
    /// <summary>
    /// Gets or sets the sensor ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the lumens measurement.
    /// </summary>
    [JsonPropertyName("lumens")]
    public double Lumens { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    [JsonPropertyName("sentAt")]
    public string SentAt { get; set; } = string.Empty;
}