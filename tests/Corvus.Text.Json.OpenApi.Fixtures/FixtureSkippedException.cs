// <copyright file="FixtureSkippedException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.Fixtures;

/// <summary>
/// Thrown when a fixture case is marked skipped for the language under test; the runner reports it as inconclusive.
/// </summary>
public sealed class FixtureSkippedException(string reason) : Exception(reason);