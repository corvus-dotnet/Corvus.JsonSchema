// <copyright file="BenchmarkPartial.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.Patch.SpecGenerator;

/// <summary>
/// The code behind for spec generation.
/// </summary>
#pragma warning disable CA1050 // Declare types in namespaces
public partial class Benchmark
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Benchmark"/> class.
    /// </summary>
    /// <param name="scenario">The JSON object containing the scenario definition.</param>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="index">The index of the scenario in the feature array.</param>
    public Benchmark(Scenario scenario, string featureName, int index)
    {
        this.Scenario = scenario;
        this.FeatureName = featureName;
        this.Index = index;
    }

    /// <summary>
    /// Gets the array of scenarios in the feature.
    /// </summary>
    public Scenario Scenario { get; }

    /// <summary>
    /// Gets the name of the feature.
    /// </summary>
    public string FeatureName { get; }

    /// <summary>
    /// Gets the scenario name.
    /// </summary>
    public string ScenarioName => (string?)this.Scenario.Comment.AsOptional() ?? "Undescribed scenario";

    /// <summary>
    /// Gets the index into the scenario array for us to generate.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Escape the json string.
    /// </summary>
    /// <param name="escape">The string to escape.</param>
    /// <returns>The escaped string.</returns>
    public string Escape(string escape)
    {
        return escape.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
