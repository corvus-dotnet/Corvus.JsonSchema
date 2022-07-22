// <copyright file="BuilderSpecPartial.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.Patch.SpecGenerator;

/// <summary>
/// The code behind for spec generation.
/// </summary>
#pragma warning disable CA1050 // Declare types in namespaces
public partial class BuilderSpec
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuilderSpec"/> class.
    /// </summary>
    /// <param name="feature">The JSON object containing the feature definition.</param>
    /// <param name="name">The name of the feature.</param>
    public BuilderSpec(ScenarioArray feature, string name)
    {
        this.Feature = feature;
        this.FeatureName = name;
    }

    /// <summary>
    /// Gets the array of scenarios in the feature.
    /// </summary>
    public ScenarioArray Feature { get; }

    /// <summary>
    /// Gets the name of the feature.
    /// </summary>
    public string FeatureName { get; }

    /// <summary>
    /// Gets the expected value from a scenario.
    /// </summary>
    /// <param name="scenario">The scenario from which to extract the expected value.</param>
    /// <returns>The expected value from the scenario.</returns>
    public static string GetExpected(Scenario scenario)
    {
        return scenario.AsScenarioWithResult.Expected.ToString();
    }
}