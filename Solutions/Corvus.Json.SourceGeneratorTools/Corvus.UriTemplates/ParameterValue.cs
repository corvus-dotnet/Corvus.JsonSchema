// <copyright file="ParameterValue.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.UriTemplates;

/// <summary>
/// A parameter value.
/// </summary>
public readonly struct ParameterValue(ParameterName name, Range valueRange)
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public ParameterName Name => name;

    /// <summary>
    /// Gets the range in the input URI string at which the parameter was found.
    /// </summary>
    internal Range ValueRange => valueRange;

    /// <summary>
    /// Gets the value of the parameter.
    /// </summary>
    /// <param name="uri">
    /// The URI containing this parameter value. This must be the same URI that was used
    /// to parse the parameter.
    /// </param>
    /// <returns>The parameter value.</returns>
    public ReadOnlySpan<char> GetValue(ReadOnlySpan<char> uri)
    {
        return uri[this.ValueRange];
    }
}