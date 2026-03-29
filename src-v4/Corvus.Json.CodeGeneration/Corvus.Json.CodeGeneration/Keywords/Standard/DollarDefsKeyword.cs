// <copyright file="DollarDefsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The $defs keyword.
/// </summary>
public sealed class DollarDefsKeyword : IDefinitionsKeyword, INonStructuralKeyword
{
    private DollarDefsKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DollarDefsKeyword"/> keyword.
    /// </summary>
    public static DollarDefsKeyword Instance { get; } = new DollarDefsKeyword();

    /// <inheritdoc />
    public string Keyword => "$defs";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "$defs"u8;

    /// <inheritdoc />
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
    {
        if (schema.TryGetKeyword(this, out JsonElement value))
        {
            Subschemas.AddSubschemasForMapOfSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary, cancellationToken);
        }
    }

    /// <inheritdoc />
    public ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        // We do not build types for $defs by default, unless they are referenced from elsewhere.
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