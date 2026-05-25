// <copyright file="BenchmarkHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace AsyncApiBenchmark.Infrastructure;

/// <summary>
/// A minimal handler implementation for benchmarking the generated consumer pipeline.
/// Accesses payload properties to force parsing but performs no other work.
/// </summary>
internal sealed class BenchmarkHandler : IReceiveLightMeasurementHandler
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static readonly BenchmarkHandler Instance = new();

    /// <inheritdoc/>
    public ValueTask HandleLightMeasuredAsync(LightMeasuredPayload payload, CancellationToken cancellationToken = default)
    {
        // Access properties to force the parse — this is what a real handler does.
        _ = payload.Id;
        _ = payload.Lumens;
        return ValueTask.CompletedTask;
    }
}