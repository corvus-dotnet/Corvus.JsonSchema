// <copyright file="SourceCredentialLifecycleTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Slice 1 of the §13.2 lifecycle layer: the binding's non-sensitive <c>expiresAt</c>/<c>rotatedAt</c> metadata
/// persists through the store write/read path, and <see cref="CredentialStatus"/> derives correctly from it.
/// </summary>
[TestClass]
public sealed class SourceCredentialLifecycleTests
{
    private static readonly DateTimeOffset Expiry = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Rotated = new(2026, 6, 1, 8, 30, 0, TimeSpan.Zero);

    private static SourceCredentialDefinition WithExpiry(string source, DateTimeOffset? expiresAt, DateTimeOffset? rotatedAt = null)
        => new(
            source,
            "production",
            SourceCredentialKind.ApiKey,
            [new SecretReferenceDefinition("value", "keyvault://petstore-apikey#3")],
            ExpiresAt: expiresAt,
            RotatedAt: rotatedAt);

    [TestMethod]
    public async Task ExpiresAt_and_RotatedAt_round_trip_through_the_store()
    {
        var store = new InMemorySourceCredentialStore();
        using (await store.AddAsync(WithExpiry("petstore", Expiry, Rotated), "alice", default))
        {
        }

        using ParsedJsonDocument<SourceCredentialBinding>? fetched =
            await store.GetAsync("petstore", "production", AccessContext.System, default);

        fetched.ShouldNotBeNull();
        fetched.RootElement.ExpiresAtOrNull.ShouldBe(Expiry);
        fetched.RootElement.RotatedAtOrNull.ShouldBe(Rotated);
    }

    [TestMethod]
    public async Task A_binding_without_lifecycle_metadata_has_null_accessors_and_is_Valid()
    {
        var store = new InMemorySourceCredentialStore();
        using (await store.AddAsync(WithExpiry("petstore", expiresAt: null), "alice", default))
        {
        }

        using ParsedJsonDocument<SourceCredentialBinding>? fetched =
            await store.GetAsync("petstore", "production", AccessContext.System, default);

        fetched.ShouldNotBeNull();
        fetched.RootElement.ExpiresAtOrNull.ShouldBeNull();
        fetched.RootElement.RotatedAtOrNull.ShouldBeNull();
        fetched.RootElement.DeriveStatus(Expiry.AddYears(1), TimeSpan.FromHours(1)).ShouldBe(CredentialStatus.Valid);
    }

    [TestMethod]
    public async Task DeriveStatus_reflects_now_against_the_expiring_window()
    {
        var store = new InMemorySourceCredentialStore();
        using (await store.AddAsync(WithExpiry("petstore", Expiry), "alice", default))
        {
        }

        using ParsedJsonDocument<SourceCredentialBinding>? fetched =
            await store.GetAsync("petstore", "production", AccessContext.System, default);
        fetched.ShouldNotBeNull();
        SourceCredentialBinding binding = fetched.RootElement;
        TimeSpan window = TimeSpan.FromHours(1);

        binding.DeriveStatus(Expiry.AddSeconds(1), window).ShouldBe(CredentialStatus.Expired);
        binding.DeriveStatus(Expiry, window).ShouldBe(CredentialStatus.Expired); // boundary: at-expiry is expired
        binding.DeriveStatus(Expiry.AddMinutes(-30), window).ShouldBe(CredentialStatus.ExpiringSoon);
        binding.DeriveStatus(Expiry - window, window).ShouldBe(CredentialStatus.ExpiringSoon); // boundary: exactly one window out
        binding.DeriveStatus(Expiry.AddHours(-2), window).ShouldBe(CredentialStatus.Valid);
    }
}