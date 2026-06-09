// <copyright file="TestTimeProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// A minimal manually-advanced <see cref="TimeProvider"/> for deterministic lease-expiry and timestamp tests.
/// </summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset now;

    public TestTimeProvider(DateTimeOffset start) => this.now = start;

    public override DateTimeOffset GetUtcNow() => this.now;

    public void Advance(TimeSpan by) => this.now += by;
}