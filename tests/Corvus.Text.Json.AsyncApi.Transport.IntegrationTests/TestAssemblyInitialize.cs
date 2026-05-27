// <copyright file="TestAssemblyInitialize.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Assembly-level initialization for integration tests.
/// </summary>
[TestClass]
public static class TestAssemblyInitialize
{
    /// <summary>
    /// Normalizes the DOCKER_HOST environment variable to work with both Podman and Docker.
    /// </summary>
    /// <param name="context">The test context.</param>
    /// <remarks>
    /// Podman on Windows uses npipe:////./pipe/podman-machine-default (4 slashes)
    /// but Docker.DotNet's NPipe handler expects npipe://./pipe/name (2 slashes).
    /// This method normalizes the URI format so Testcontainers works with both.
    /// </remarks>
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        string? dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");

        if (!string.IsNullOrEmpty(dockerHost) && dockerHost.StartsWith("npipe:////", StringComparison.OrdinalIgnoreCase))
        {
            // Normalize Podman's npipe:////./pipe/name to npipe://./pipe/name
            string normalized = "npipe://" + dockerHost.Substring("npipe:////".Length);
            Environment.SetEnvironmentVariable("DOCKER_HOST", normalized);
            Console.WriteLine($"[TestAssemblyInitialize] Normalized DOCKER_HOST from '{dockerHost}' to '{normalized}'");
        }
    }
}