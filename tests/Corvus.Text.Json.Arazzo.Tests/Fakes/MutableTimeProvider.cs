// <copyright file="MutableTimeProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Tests.Fakes;

/// <summary>A manually-advanced <see cref="TimeProvider"/> for deterministic durable-timer tests.</summary>
public sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => this.now;

    public void Advance(TimeSpan by) => this.now += by;
}