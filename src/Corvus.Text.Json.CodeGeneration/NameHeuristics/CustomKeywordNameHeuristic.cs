// <copyright file="CustomKeywordNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Linq;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

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