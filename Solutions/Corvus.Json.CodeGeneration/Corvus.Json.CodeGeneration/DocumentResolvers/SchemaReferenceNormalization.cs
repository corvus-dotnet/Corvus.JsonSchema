// <copyright file="SchemaReferenceNormalization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration.DocumentResolvers;

/// <summary>
/// Normalizes a reference to a schema file.
/// </summary>
public static class SchemaReferenceNormalization
{
    /// <summary>
    /// Normalizes a schema reference.
    /// </summary>
    /// <param name="schemaFile">The reference to the schema file.</param>
    /// <param name="result">The resulting reference URI.</param>
    /// <returns><see langword="true"/> if the reference was successfully normalized.</returns>
    public static bool TryNormalizeSchemaReference(string schemaFile, [NotNullWhen(true)] out string? result)
    {
        return TryNormalizeSchemaReference(schemaFile, string.Empty, out result);
    }

    /// <summary>
    /// Normalizes a schema reference.
    /// </summary>
    /// <param name="schemaFile">The reference to the schema file.</param>
    /// <param name="basePath">The current base path, for relative references.</param>
    /// <param name="result">The resulting reference URI.</param>
    /// <returns><see langword="true"/> if the reference was successfully normalized.</returns>
    public static bool TryNormalizeSchemaReference(string schemaFile, string basePath, [NotNullWhen(true)] out string? result)
    {
        if (!IsValid(schemaFile, out Uri? uri) || uri.IsFile)
        {
            if (IsPartiallyQualified(schemaFile.AsSpan()))
            {
                if (!string.IsNullOrEmpty(basePath))
                {
                    schemaFile = Path.Combine(basePath, schemaFile);
                }

                schemaFile = Path.GetFullPath(schemaFile);
            }

            result = schemaFile.Replace('\\', '/');
            return true;
        }

        result = null;
        return false;
        static bool IsDirectorySeparator(char c)
        {
            if (c != Path.DirectorySeparatorChar)
            {
                return c == Path.AltDirectorySeparatorChar;
            }

            return true;
        }

        static bool IsPartiallyQualified(ReadOnlySpan<char> path)
        {
            if (path.Length < 2)
            {
                return true;
            }

            if (IsDirectorySeparator(path[0]))
            {
                if (path[1] != '?')
                {
                    return !IsDirectorySeparator(path[1]);
                }

                return false;
            }

            if (path.Length >= 3 && path[1] == Path.VolumeSeparatorChar && IsDirectorySeparator(path[2]))
            {
                return !IsValidDriveChar(path[0]);
            }

            return true;
        }

        static bool IsValidDriveChar(char value)
        {
            return (uint)((value | 0x20) - 97) <= 25u;
        }

        static bool IsValid(string uriString, [NotNullWhen(true)] out Uri? uri)
        {
            return TryGetUri(uriString, out uri) && (!uri.IsAbsoluteUri || !uri.IsUnc);
        }

        static bool TryGetUri(string text, [NotNullWhen(true)] out Uri? value)
        {
            return Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out value) && value.IsAbsoluteUri;
        }
    }
}