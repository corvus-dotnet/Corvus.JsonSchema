// <copyright file="SpecPartial.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Corvus.Json;

/// <summary>
/// The code behind for spec generation.
/// </summary>
#pragma warning disable CA1050 // Declare types in namespaces
public partial class Spec
#pragma warning restore CA1050 // Declare types in namespaces
{
    private readonly JsonObject feature;

    /// <summary>
    /// Initializes a new instance of the <see cref="Spec"/> class.
    /// </summary>
    /// <param name="feature">The JSON object containing the feature definition.</param>
    /// <param name="name">The name of the feature.</param>
    public Spec(JsonObject feature, string name)
    {
        this.feature = feature;
        this.FeatureName = name;
    }

    /// <summary>
    /// Gets the name of the feature.
    /// </summary>
    public string FeatureName { get; }

    /// <summary>
    /// Gets the scenarios in the feature.
    /// </summary>
    public IEnumerable<Scenario> Scenarios
    {
        get
        {
           foreach (Property scenario in this.feature.EnumerateObject())
            {
                yield return new Scenario(scenario);
            }
        }
    }
}
