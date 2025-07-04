﻿// <copyright file="RequiredPropertyNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on required properties value.
/// </summary>
public sealed class RequiredPropertyNameHeuristic : INameHeuristicBeforeSubschema
{
    private RequiredPropertyNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="RequiredPropertyNameHeuristic"/>.
    /// </summary>
    public static RequiredPropertyNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => true;

    /// <inheritdoc/>
    public uint Priority => 1550;

    private static ReadOnlySpan<char> RequiredPropertyPrefix => "Required".AsSpan();

    private static ReadOnlySpan<char> RequiredPropertySeparator => "And".AsSpan();

    /// <inheritdoc/>
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.Parent() is null || typeDeclaration.IsInDefinitionsContainer())
        {
            written = 0;
            return false;
        }

        int count = 0;
        written = 0;
        foreach (PropertyDeclaration property in
                    typeDeclaration.PropertyDeclarations
                        .Where(p =>
                            p.RequiredOrOptional != RequiredOrOptional.Optional))
        {
            if (count > 3)
            {
                return false;
            }

            count++;

            if (written == 0)
            {
                RequiredPropertyPrefix.CopyTo(typeNameBuffer);
                written = RequiredPropertyPrefix.Length;
            }
            else
            {
                RequiredPropertySeparator.CopyTo(typeNameBuffer[written..]);
                written += RequiredPropertySeparator.Length;
            }

            written += Formatting.FormatTypeNameComponent(typeDeclaration, property.JsonPropertyName.AsSpan(), typeNameBuffer[written..]);
        }

        if (written > 0 && typeDeclaration.CollidesWithParent(typeNameBuffer[..written]))
        {
            written = 0;
        }

        return written > 0;
    }
}