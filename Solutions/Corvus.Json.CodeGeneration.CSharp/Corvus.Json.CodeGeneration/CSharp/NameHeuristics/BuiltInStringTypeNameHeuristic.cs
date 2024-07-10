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
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        written = 0;

        if ((typeDeclaration.AllowedCoreTypes() & CoreTypes.String) != 0 &&
            typeDeclaration.AllowedCoreTypes().CountTypes() == 1)
        {
            string? candidateFormat = null;
            string? candidateContentEncoding = null;
            string? candidateContentMediaType = null;
            ContentEncodingSemantics? contentSemantics = null;

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
                    continue;
                }

                if (keyword is IContentEncodingProviderKeyword contentEncodingKeyword)
                {
                    contentEncodingKeyword.TryGetContentEncoding(typeDeclaration, out candidateContentEncoding);
                    contentSemantics = contentEncodingKeyword.ContentSemantics;
                    continue;
                }

                if (keyword is IContentMediaTypeProviderKeyword contentMediaTypeKeyword)
                {
                    contentMediaTypeKeyword.TryGetContentMediaType(typeDeclaration, out candidateContentMediaType);
                    continue;
                }

                // This is "some other" structural keyword, so we can't continue.
                return false;
            }

            typeDeclaration.SetDotnetNamespace("Corvus.Json");

            if (candidateFormat is string format)
            {
                typeDeclaration.SetDotnetNamespace(WellKnownStringFormatHelpers.GetDotnetTypeNameFor(format) ?? "JsonString");
            }
            else if (
                candidateContentMediaType is string contentMediaType &&
                contentMediaType == "application/json")
            {
                if (candidateContentEncoding is string contentEncoding)
                {
                    if (contentEncoding == "base64")
                    {
                        typeDeclaration.SetDotnetTypeName("JsonBase64Content");
                    }
                    else
                    {
                        typeDeclaration.SetDotnetTypeName("JsonString");
                    }
                }
                else
                {
                    typeDeclaration.SetDotnetTypeName("JsonContent");
                }
            }
            else if (
                candidateContentEncoding is string contentEncoding &&
                contentEncoding == "base64")
            {
                if (contentSemantics == ContentEncodingSemantics.PreDraft201909)
                {
                    typeDeclaration.SetDotnetTypeName("JsonBase64StringPre201909");
                }
                else
                {
                    typeDeclaration.SetDotnetTypeName("JsonBase64String");
                }
            }
            else
            {
                // This is a simple string
                typeDeclaration.SetDotnetTypeName("JsonString");
            }

            return true;
        }

        return false;
    }
}