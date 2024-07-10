// <copyright file="DefinitionsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The definitions keyword.
/// </summary>
public sealed class DefinitionsKeyword : IDefinitionsKeyword, INonStructuralKeyword
{
    private DefinitionsKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DefinitionsKeyword"/> keyword.
    /// </summary>
    public static DefinitionsKeyword Instance { get; } = new DefinitionsKeyword();

    /// <inheritdoc />
    public string Keyword => "definitions";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "definitions"u8;

    /// <inheritdoc />
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary)
    {
        if (schema.TryGetKeyword(this, out JsonElement value))
        {
            Subschemas.AddSubschemasForMapOfSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary);
        }
    }

    /// <inheritdoc />
    public ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        // We do not build types for definitions by default, unless they are referenced from elsewhere.
#if NET8_0_OR_GREATER
        return ValueTask.CompletedTask;
#else
        return new ValueTask(Task.CompletedTask);
#endif
    }

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;
}