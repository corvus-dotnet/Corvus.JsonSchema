// <copyright file="BuiltInStringTypeNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on a built-in string type.
/// </summary>
public sealed class BuiltInStringTypeNameHeuristic : IBuiltInTypeNameHeuristic
{
    private BuiltInStringTypeNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="BuiltInStringTypeNameHeuristic"/>.
    /// </summary>
    public static BuiltInStringTypeNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 1;

    /// <inheritdoc/>
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        written = 0;

        if ((typeDeclaration.AllowedCoreTypes() & CoreTypes.String) != 0 &&
            typeDeclaration.AllowedCoreTypes().CountTypes() == 1 &&
            !typeDeclaration.UseImplicitOperatorString())
        {
            string? candidateFormat = null;
            bool assertFormat = typeDeclaration.AlwaysAssertFormat();

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

                if (keyword is IFormatProviderKeyword formatKeyword)
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
                    typeDeclaration.SetDotnetTypeName(FormatHandlerRegistry.Instance.StringFormatHandlers.GetCorvusJsonTypeNameFor(format) ?? "JsonString");
                    return true;
                }
            }
            else
            {
                // If we do not have a format then this is a simple string.
                typeDeclaration.SetDotnetNamespace("Corvus.Json");
                typeDeclaration.SetDotnetTypeName("JsonString");
                return true;
            }
        }

        return false;
    }
}