// <copyright file="CustomKeywordNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using CommunityToolkit.HighPerformance;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on a CustomKeyword index.
/// </summary>
public sealed class CustomKeywordNameHeuristic : INameHeuristicBeforeSubschema
{
    private CustomKeywordNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="CustomKeywordNameHeuristic"/>.
    /// </summary>
    public static CustomKeywordNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 100;

    /// <inheritdoc/>
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.TypeNameProviderKeywords().FirstOrDefault() is ITypeNameProviderKeyword typeNameProviderKeyword &&
            typeDeclaration.LocatedSchema.Schema.TryGetProperty(typeNameProviderKeyword.KeywordUtf8, out JsonElement valueElement) &&
            valueElement.ValueKind == JsonValueKind.String &&
            valueElement.GetString() is string value)
        {
            written = Formatting.FormatTypeNameComponent(typeDeclaration, value.AsSpan(), typeNameBuffer);

            if (!typeDeclaration.CollidesWithParent(typeNameBuffer[..written]))
            {
                return true;
            }
        }

        written = 0;
        return false;
    }
}