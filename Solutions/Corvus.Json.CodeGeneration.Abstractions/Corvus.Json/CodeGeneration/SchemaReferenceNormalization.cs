// <copyright file="SchemaReferenceNormalization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Helpers for reference normalization.
/// </summary>
public static class SchemaReferenceNormalization
{
    /// <summary>
    /// Attempts to normalize the form of a reference to a schema file.
    /// </summary>
    /// <param name="schemaFile">The schema file reference.</param>
    /// <param name="result">The result of the operation.</param>
    /// <returns><see langword="true"/> if the result required normalization, otherwise false.</returns>
    public static bool TryNormalizeSchemaReference(string schemaFile, [NotNullWhen(true)] out string? result)
    {
        JsonUri uri = new(schemaFile);
        if (!uri.IsValid() || uri.GetUri().IsFile)
        {
            // If this is, in fact, a local file path, not a uri, then convert to a fullpath and URI-style separators.
            if (!Path.IsPathFullyQualified(schemaFile))
            {
                schemaFile = Path.GetFullPath(schemaFile);
            }

            result = schemaFile.Replace('\\', '/');
            return true;
        }

        result = null;
        return false;
    }
}