// <copyright file="FixtureAssertionException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.Fixtures;

/// <summary>
/// Thrown when a fixture case's expectation is not met.
/// </summary>
public sealed class FixtureAssertionException(string message) : Exception(message);