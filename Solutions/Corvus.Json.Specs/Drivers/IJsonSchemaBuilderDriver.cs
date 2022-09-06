// <copyright file="IJsonSchemaBuilderDriver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;

namespace Drivers;

/// <summary>
/// A driver for a Json Schema Builder.
/// </summary>
public interface IJsonSchemaBuilderDriver : IDisposable
{
    /// <summary>
    /// Create an instance of the given <see cref="IJsonValue"/> type from
    /// the json data provided.
    /// </summary>
    /// <param name="type">The type (which must be a <see cref="IJsonValue"/> and have a constructor with a single <see cref="JsonElement"/> parameter.</param>
    /// <param name="data">The JSON data from which to initialize the value.</param>
    /// <returns>An instance of a <see cref="IJsonValue"/> initialized from the data.</returns>
    IJsonValue CreateInstance(Type type, JsonElement data);

    /// <summary>
    /// Create an instance of the given <see cref="IJsonValue"/> type from
    /// the json data provided.
    /// </summary>
    /// <param name="type">The type (which must be a <see cref="IJsonValue"/> and have a constructor with a single <see cref="JsonElement"/> parameter.</param>
    /// <param name="data">The JSON data from which to initialize the value.</param>
    /// <returns>An instance of a <see cref="IJsonValue"/> initialized from the data.</returns>
    IJsonValue CreateInstance(Type type, string data);

    /// <summary>
    /// Generates a type for the given root Schema element.
    /// </summary>
    /// <param name="writeBenchmarks">If <c>true</c>, write benchmark files.</param>
    /// <param name="index">The index of the scenario example.</param>
    /// <param name="filename">The filename containing the Schema.</param>
    /// <param name="schemaPath">The path to the Schema in the file.</param>
    /// <param name="dataPath">The path to the data in the file.</param>
    /// <param name="featureName">The feature name for the type.</param>
    /// <param name="scenarioName">The scenario name for the type.</param>
    /// <param name="valid">Whether the scenario is expected to be valid.</param>
    /// <returns>The fully qualified type name of the entity we have generated.</returns>
    Task<Type> GenerateTypeFor(bool writeBenchmarks, int index, string filename, string schemaPath, string dataPath, string featureName, string scenarioName, bool valid);

    /// <summary>
    /// Get the <see cref="JsonElement"/> at the given reference location.
    /// </summary>
    /// <param name="filename">The name of the file containing the element.</param>
    /// <param name="referenceFragment">The local reference to the element in the file.</param>
    /// <returns>The element, found in the specified document.</returns>
    Task<JsonElement?> GetElement(string filename, string referenceFragment);
}