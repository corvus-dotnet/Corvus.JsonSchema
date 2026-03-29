// <copyright file="ValidationContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Corvus.Text.Json.Compatibility;

/// <summary>
/// Represents the context for a JSON schema validation operation, including validity and results.
/// </summary>
public readonly struct ValidationContext
{
    /// <summary>
    /// Gets an invalid context.
    /// </summary>
    public static readonly ValidationContext InvalidContext = new(false);

    /// <summary>
    /// Gets a valid context.
    /// </summary>
    public static readonly ValidationContext ValidContext = new(true);

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationContext"/> struct.
    /// </summary>
    /// <param name="isValid">A value indicating whether the context is valid.</param>
    internal ValidationContext(bool isValid)
    {
        IsValid = isValid;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationContext"/> struct.
    /// </summary>
    /// <param name="isMatch">A value indicating whether the validation was a match.</param>
    /// <param name="collector">The results collector containing validation data.</param>
    // This is the constructor for when the results collection has not changed
    internal ValidationContext(bool isMatch, JsonSchemaResultsCollector collector)
    {
        // Capture it at the moment we were given the collector
        IsValid = isMatch;
        Collector = collector;
    }

    /// <summary>
    /// Gets a value indicating whether this context represents a valid result.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the validation results.
    /// </summary>
    public IReadOnlyList<ValidationResult> Results => BuildResults(Collector);

    /// <summary>
    /// Gets the internal results collector.
    /// </summary>
    internal JsonSchemaResultsCollector? Collector { get; }

    internal static JsonSchemaResultsLevel MapLevel(ValidationLevel level)
    {
        return level switch
        {
            // Do not allow Flag
            ValidationLevel.Basic => JsonSchemaResultsLevel.Basic,
            ValidationLevel.Detailed => JsonSchemaResultsLevel.Detailed,
            ValidationLevel.Verbose => JsonSchemaResultsLevel.Verbose,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
        };
    }

    private static ReadOnlyCollection<ValidationResult> BuildResults(JsonSchemaResultsCollector? collector)
    {
        if (collector is null)
        {
            return new ReadOnlyCollection<ValidationResult>(Array.Empty<ValidationResult>());
        }

        var result = new List<ValidationResult>();
        int index = 0;
        JsonSchemaResultsCollector.ResultsEnumerator enumerator = collector.EnumerateResults();
        while (enumerator.MoveNext())
        {
            result.Add(new ValidationResult(collector, index));
            index++;
        }

        return new ReadOnlyCollection<ValidationResult>(result);
    }
}