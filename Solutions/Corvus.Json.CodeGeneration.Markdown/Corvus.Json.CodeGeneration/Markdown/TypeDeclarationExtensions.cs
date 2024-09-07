// <copyright file="TypeDeclarationExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration.Markdown;

/// <summary>
/// Extensions for <see cref="TypeDeclaration"/>.
/// </summary>
public static class TypeDeclarationExtensions
{
    private const string NameKey = "CorvusMarkdown_Name";
    private const string AdditionalDocumentationKey = "CorvusMarkdown_AdditionalDocumentation";

    /// <summary>
    /// Gets the unique name for the type declaration.
    /// </summary>
    /// <param name="type">The <see cref="TypeDeclaration"/> for which to get the name.</param>
    /// <param name="existingNames">The existing names.</param>
    /// <returns>The type declaration's unique name.</returns>
    public static string Name(this TypeDeclaration type, HashSet<string> existingNames)
    {
        if (!type.TryGetMetadata(NameKey, out string? name) || name is null)
        {
            name = GetName(type, existingNames);
            type.SetMetadata(NameKey, name);
        }

        return name;

        static string GetName(TypeDeclaration type, HashSet<string> existingNames)
        {
            Span<char> buffer = stackalloc char[type.LocatedSchema.Location.Length + 6];
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Try to get the additional documentation for the type.
    /// </summary>
    /// <param name="type">The <see cref="TypeDeclaration"/> for which to get additional documentation.</param>
    /// <param name="result">The additional documentation for the type (if available).</param>
    /// <returns><see langword="true"/> if additional documentation was available for the type.</returns>
    public static bool TryGetAdditionalDocumentation(this TypeDeclaration type, [NotNullWhen(true)] out AdditionalSchemaDocumentation.Documentation result)
    {
        return type.TryGetMetadata(AdditionalDocumentationKey, out result);
    }

    /// <summary>
    /// Get the additional documentation details for the given type and path.
    /// </summary>
    /// <param name="type">The type declaration for which to retrieve the additional documentation details.</param>
    /// <param name="path">The path within the schema for which to get additional details.</param>
    /// <returns>The <see cref="AdditionalSchemaDocumentation.Details"/>, or <see langword="null"/> if no details are available.</returns>
    public static AdditionalSchemaDocumentation.Details? GetDocumentationDetails(this TypeDeclaration type, ReadOnlySpan<byte> path)
    {
        if (type.TryGetAdditionalDocumentation(out AdditionalSchemaDocumentation.Documentation additionalSchemaDocumention) &&
            additionalSchemaDocumention.TryGetProperty(path, out AdditionalSchemaDocumentation.Details details))
        {
            return details;
        }

        return null;
    }

    /// <summary>
    /// Get the additional documentation details for the given type and path.
    /// </summary>
    /// <param name="type">The type declaration for which to retrieve the additional documentation details.</param>
    /// <param name="path">The path within the schema for which to get additional details.</param>
    /// <returns>The <see cref="AdditionalSchemaDocumentation.Details"/>, or <see langword="null"/> if no details are available.</returns>
    public static AdditionalSchemaDocumentation.Details? GetDocumentationDetails(this TypeDeclaration type, string path)
    {
        if (type.TryGetAdditionalDocumentation(out AdditionalSchemaDocumentation.Documentation additionalSchemaDocumention) &&
            additionalSchemaDocumention.TryGetProperty(path, out AdditionalSchemaDocumentation.Details details))
        {
            return details;
        }

        return null;
    }

    /// <summary>
    /// Sets the markdown language provider options on the type declaration.
    /// </summary>
    /// <param name="type">The type declaration on which to set the options.</param>
    /// <param name="options">The options to set.</param>
    public static void SetOptions(this TypeDeclaration type, MarkdownLanguageProvider.Options options)
    {
        if (!type.TryGetMetadata(
            AdditionalDocumentationKey,
            out AdditionalSchemaDocumentation.Documentation? _))
        {
            if (options.AdditionalSchemaDocumentation is AdditionalSchemaDocumentation asd &&
                asd.TryGetProperty(type.LocatedSchema.Location, out AdditionalSchemaDocumentation.Documentation doc))
            {
                type.SetMetadata(AdditionalDocumentationKey, doc);
            }
            else
            {
                type.SetMetadata(AdditionalDocumentationKey, default(AdditionalSchemaDocumentation.Documentation?));
            }
        }
    }
}