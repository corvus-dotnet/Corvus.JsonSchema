// <copyright file="Variable.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json;

/// <summary>
/// Wraps a <see cref="Property"/> to create a variable.
/// </summary>
public readonly struct Variable
{
    private readonly Property variable;

    /// <summary>
    /// Initializes a new instance of the <see cref="Variable"/> struct.
    /// </summary>
    /// <param name="variable">The <see cref="Property"/> backing.</param>
    public Variable(Property variable)
    {
        this.variable = variable;
    }

    /// <summary>
    /// Gets the scenario name.
    /// </summary>
    public string Name
    {
        get
        {
            return this.variable.Name;
        }
    }

    /// <summary>
    /// Gets a serialzed JSON string representing the parameter value.
    /// </summary>
    public string Value
    {
        get
        {
            return Encoding.UTF8.GetString(this.variable.Value.GetRawText(new JsonWriterOptions { Indented = false })).Replace("|", "\\|");
        }
    }
}
