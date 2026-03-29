// <copyright file="SpecPartial.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.Patch.SpecGenerator;

/// <summary>
/// The code behind for spec generation.
/// </summary>
public partial class Spec
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Spec"/> class.
    /// </summary>
    /// <param name="feature">The JSON object containing the feature definition.</param>
    /// <param name="name">The name of the feature.</param>
    public Spec(Feature feature, string name)
    {
        this.Feature = feature;
        this.FeatureName = name;
    }

    /// <summary>
    /// Gets the array of scenarios in the feature.
    /// </summary>
    public Feature Feature { get; }

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