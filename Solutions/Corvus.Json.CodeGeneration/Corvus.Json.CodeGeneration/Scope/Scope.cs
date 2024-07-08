// <copyright file="Scope.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Scope-related helpers.
/// </summary>
public static class Scope
{
    /// <summary>
    /// Determines if the current vocabulary indicates that we should enter a new scope.
    /// </summary>
    /// <param name="schema">The current schema.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <param name="scopeName">The new scope name.</param>
    /// <returns><see langword="true"/> if we should enter a new scope.</returns>
    public static bool ShouldEnterScope(JsonElement schema, IVocabulary vocabulary, [NotNullWhen(true)] out string? scopeName)
    {
        return ShouldEnterScope(schema, vocabulary, out scopeName, out _);
    }

    /// <summary>
    /// Try to enter the scope for the type declaration.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="existingTypeDeclaration">The existing type declaration for the scope,
    /// if a scope was entered and a type declaration was built for the scope.</param>
    /// <returns><see langword="true"/> if the scope was entered.</returns>
    public static bool TryEnterScope(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, out TypeDeclaration? existingTypeDeclaration)
    {
        if (!ShouldEnterScope(typeDeclaration.LocatedSchema.Schema, typeDeclaration.LocatedSchema.Vocabulary, out string? scopeName, out IScopeKeyword? scopeKeyword))
        {
            existingTypeDeclaration = null;
            return false;
        }

        CustomKeywords.ApplyBeforeEnteringScope(typeBuilderContext, typeDeclaration);

        bool result = scopeKeyword.TryEnterScope(typeBuilderContext, typeDeclaration, scopeName, out existingTypeDeclaration);

        if (result)
        {
            CustomKeywords.ApplyAfterEnteringScope(typeBuilderContext, existingTypeDeclaration ?? typeDeclaration);
        }

        return result;
    }

    private static bool ShouldEnterScope(JsonElement schema, IVocabulary vocabulary, [NotNullWhen(true)] out string? scopeName, [NotNullWhen(true)] out IScopeKeyword? scopeKeyword)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            scopeName = default;
            scopeKeyword = default;
            return false;
        }

        // If we have any keywords that hide siblings, and they are not a scope-provider keyword.
        if (vocabulary.Keywords.Any(
            k =>
                k is not IScopeKeyword &&
                k is IHidesSiblingsKeyword &&
                schema.HasKeyword(k)))
        {
            scopeName = default;
            scopeKeyword = default;
            return false;
        }

        // We will enter a scope based on the first scope keyword we find if there are several.
        foreach (IScopeKeyword keyword in vocabulary.Keywords.OfType<IScopeKeyword>())
        {
            if (keyword.TryGetScope(schema, out scopeName))
            {
                scopeKeyword = keyword;
                return true;
            }
        }

        scopeName = default;
        scopeKeyword = default;
        return false;
    }
}