// <copyright file="ISchemaRegistrationCustomKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A custom keyword that is applied during schema registration phase.
/// </summary>
/// <remarks>
/// Note that a custom keyword may well apply in both the <see cref="ISchemaRegistrationCustomKeyword"/> phase
/// and the <see cref="ITypeBuilderCustomKeyword"/> phase.
/// </remarks>
public interface ISchemaRegistrationCustomKeyword
{
    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    void Apply(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken);
}