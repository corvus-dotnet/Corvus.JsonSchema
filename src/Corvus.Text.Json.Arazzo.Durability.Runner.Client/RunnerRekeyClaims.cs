// <copyright file="RunnerRekeyClaims.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>One page of the re-key sweep's claims (ADR 0065 decision 12): the runs leased to this runner, and where the next pass continues.</summary>
/// <param name="Claims">The claimed runs, each already leased.</param>
/// <param name="NextPageToken">The token for the next pass, or <see langword="null"/> when the walk reached the end of the candidate set.</param>
public readonly record struct RunnerRekeyClaims(IReadOnlyList<RunnerClaim> Claims, string? NextPageToken);