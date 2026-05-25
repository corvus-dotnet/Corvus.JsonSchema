// <copyright file="WolverineBaseline.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
/// This gives a second comparison point: what does a popular .NET message bus
/// framework cost vs raw STJ (floor) vs Corvus (our pipeline)?
/// </para>
/// <para>
/// The benchmark includes STJ deserialization + Wolverine dispatch + handler
/// property access — the full cost a Wolverine user pays.
/// </para>
/// </remarks>
public sealed class WolverineBaseline : IAsyncDisposable
{
    private IHost host = null!;
    private IMessageBus bus = null!;

    /// <summary>
    /// Configures Wolverine with a single handler and starts the host.
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
    /// Dispatches a pre-deserialized message through Wolverine's handler pipeline.
    /// </summary>
    /// <param name="message">The deserialized POCO message.</param>
    /// <returns>A task representing the async operation.</returns>
    public Task InvokeAsync(LightMeasuredPoco message)
    {
        return this.bus.InvokeAsync(message);
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
        // Access properties (prevents dead-code elimination of deserialization)
        _ = message.Id;
        _ = message.Lumens;
        _ = message.SentAt;
    }
}