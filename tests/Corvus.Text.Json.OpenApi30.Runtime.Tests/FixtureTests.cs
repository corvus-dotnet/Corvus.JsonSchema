// <copyright file="FixtureTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests30.Client;
using Corvus.Text.Json.OpenApi.Fixtures;

namespace Corvus.Text.Json.OpenApi30.Runtime.Tests;

/// <summary>
/// Runs the language-neutral fixture cases for canonicalization-spec-3.0.json through the generated C# client.
/// </summary>
[TestClass]
public class FixtureTests
{
    public static IEnumerable<object[]> Cases => FixtureCases.Enumerate("canonicalization-spec-3.0");

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public async Task Case(string file, int index, string name)
    {
        using ParsedJsonDocument<JsonElement> fixture = FixtureCases.Load(file);
        try
        {
            await FixtureRunner.RunAsync(fixture.RootElement, index, FixtureAdapter.InvokeAsync);
        }
        catch (FixtureSkippedException skipped)
        {
            Assert.Inconclusive($"{name}: {skipped.Message}");
        }
    }
}