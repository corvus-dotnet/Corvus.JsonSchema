// <copyright file="ILocalSubschemaRegistrationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that finds and registers local subschema.
/// </summary>
public interface ILocalSubschemaRegistrationKeyword : IKeyword
{
    /// <summary>
    /// Adds local subschemas for the keyword. This is not expected to resolve references.
    /// </summary>
    /// <param name="registry">The JSON schema registry.</param>
    /// <param name="schema">The parent schema containing the keyword.</param>
    /// <param name="currentLocation">The current location in the tree.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary);
}