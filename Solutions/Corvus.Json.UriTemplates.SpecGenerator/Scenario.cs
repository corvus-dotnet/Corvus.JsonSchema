// <copyright file="Scenario.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;

/// <summary>
/// Wraps a <see cref="Property"/> to create a scenario.
/// </summary>
#pragma warning disable CA1050 // Declare types in namespaces
public readonly struct Scenario
#pragma warning restore CA1050 // Declare types in namespaces
{
    private readonly Property scenario;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scenario"/> struct.
    /// </summary>
    /// <param name="scenario">The <see cref="Property"/> backing.</param>
    public Scenario(Property scenario)
    {
        this.scenario = scenario;
    }

    /// <summary>
    /// Gets the scenario name.
    /// </summary>
    public string Name
    {
        get
        {
            return this.scenario.Name;
        }
    }

    /// <summary>
    /// Gets the level.
    /// </summary>
    public int Level
    {
        get
        {
            if (this.scenario.Value.TryGetProperty("level", out JsonAny val) && val.ValueKind == JsonValueKind.Number)
            {
                return val.AsNumber;
            }

            return 0;
        }
    }

    /// <summary>
    /// Gets the variables.
    /// </summary>
    public IEnumerable<Variable> Variables
    {
        get
        {
            if (this.scenario.Value.TryGetProperty("variables", out JsonAny val) && val.ValueKind == JsonValueKind.Object)
            {
                foreach (Property variable in val.EnumerateObject())
                {
                    yield return new Variable(variable);
                }
            }
        }
    }

    /// <summary>
    /// Gets the test cases.
    /// </summary>
    public IEnumerable<TestCase> TestCases
    {
        get
        {
            if (this.scenario.Value.TryGetProperty("testcases", out JsonAny val) && val.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonArray testcase in val.EnumerateArray())
                {
                    if (testcase.Length != 2)
                    {
                        yield break;
                    }

                    yield return new TestCase(testcase);
                }
            }
        }
    }
}
