﻿// <copyright file="BuiltInIntegerTypeNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on a built-in number type.
/// </summary>
public sealed class BuiltInIntegerTypeNameHeuristic : IBuiltInTypeNameHeuristic
{
    private BuiltInIntegerTypeNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="BuiltInIntegerTypeNameHeuristic"/>.
    /// </summary>
    public static BuiltInIntegerTypeNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 1;

    /// <inheritdoc/>
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        written = 0;
        bool assertFormat = typeDeclaration.AlwaysAssertFormat();

        if ((typeDeclaration.AllowedCoreTypes() & CoreTypes.Integer) != 0 &&
            typeDeclaration.AllowedCoreTypes().CountTypes() == 1)
        {
            string? candidateFormat = null;

            // We are a simple string type
            foreach (IKeyword keyword in typeDeclaration.Keywords())
            {
                if (keyword is ICoreTypeValidationKeyword)
                {
                    continue;
                }

                if (keyword is INonStructuralKeyword)
                {
                    // Don't worry if it is a non-structural keyword.
                    continue;
                }

                if (keyword is IFormatValidationKeyword formatKeyword)
                {
                    formatKeyword.TryGetFormat(typeDeclaration, out candidateFormat);
                    assertFormat |= keyword is IFormatValidationKeyword;
                    continue;
                }

                // This is "some other" structural keyword, so we can't continue.
                return false;
            }

            if (candidateFormat is string format)
            {
                // If we have a format and are asserting format, then set the built in type.
                // otherwise fall through to code gen so we get the formatted conversions etc
                // but we do not assert the format.
                if (assertFormat)
                {
                    typeDeclaration.SetDotnetNamespace("Corvus.Json");
                    typeDeclaration.SetDotnetTypeName(
                        FormatHandlerRegistry.Instance.NumberFormatHandlers.GetIntegerCorvusJsonTypeNameFor(format) ?? "JsonInteger");
                    return true;
                }
            }
            else
            {
                // If we do not have a format then this is a simple integer.
                typeDeclaration.SetDotnetNamespace("Corvus.Json");
                typeDeclaration.SetDotnetTypeName("JsonInteger");
                return true;
            }
        }

        return false;
    }
}