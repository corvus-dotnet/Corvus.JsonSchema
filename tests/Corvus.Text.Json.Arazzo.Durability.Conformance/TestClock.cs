// <copyright file="TestClock.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>A manually-advanced <see cref="TimeProvider"/> for deterministic lease-expiry conformance tests.</summary>
/// <param name="start">The initial instant.</param>
public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => this.now;

    /// <summary>Advances the clock.</summary>
    /// <param name="by">How far to advance.</param>
    public void Advance(TimeSpan by) => this.now += by;
}