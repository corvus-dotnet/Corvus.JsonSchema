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
            foreach (IValidationKeyword keyword in typeDeclaration.ValidationKeywords())
            {
                if (keyword is ICoreTypeValidationKeyword)
                {
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

                // This is "some other" validation keyword, so we can't continue.
                return false;
            }

            typeDeclaration.SetDotnetNamespace("Corvus.Json");

            if (candidateFormat is string format)
            {
                switch (format)
                {
                    case "date":
                        typeDeclaration.SetDotnetTypeName("JsonDate");
                        break;
                    case "date-time":
                        typeDeclaration.SetDotnetTypeName("JsonDateTime");
                        break;
                    case "time":
                        typeDeclaration.SetDotnetTypeName("JsonTime");
                        break;
                    case "duration":
                        typeDeclaration.SetDotnetTypeName("JsonDuration");
                        break;
                    case "email":
                        typeDeclaration.SetDotnetTypeName("JsonEmail");
                        break;
                    case "idn-email":
                        typeDeclaration.SetDotnetTypeName("JsonIdnEmail");
                        break;
                    case "hostname":
                        typeDeclaration.SetDotnetTypeName("JsonHostname");
                        break;
                    case "idn-hostname":
                        typeDeclaration.SetDotnetTypeName("JsonIdnHostname");
                        break;
                    case "ipv4":
                        typeDeclaration.SetDotnetTypeName("JsonIpV4");
                        break;
                    case "ipv6":
                        typeDeclaration.SetDotnetTypeName("JsonIpV6");
                        break;
                    case "uuid":
                        typeDeclaration.SetDotnetTypeName("JsonUuid");
                        break;
                    case "uri":
                        typeDeclaration.SetDotnetTypeName("JsonUri");
                        break;
                    case "uri-template":
                        typeDeclaration.SetDotnetTypeName("JsonUriTemplate");
                        break;
                    case "uri-reference":
                        typeDeclaration.SetDotnetTypeName("JsonUriReference");
                        break;
                    case "iri":
                        typeDeclaration.SetDotnetTypeName("JsonIri");
                        break;
                    case "iri-reference":
                        typeDeclaration.SetDotnetTypeName("JsonIriReference");
                        break;
                    case "json-pointer":
                        typeDeclaration.SetDotnetTypeName("JsonPointer");
                        break;
                    case "relative-json-pointer":
                        typeDeclaration.SetDotnetTypeName("JsonRelativePointer");
                        break;
                    case "regex":
                        typeDeclaration.SetDotnetTypeName("JsonRegex");
                        break;
                    default:
                        typeDeclaration.SetDotnetTypeName("JsonString");
                        break;
                }
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