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
#if NET8_0_OR_GREATER
            if (!Path.IsPathFullyQualified(schemaFile))
#else
            if (IsPartiallyQualified(schemaFile.AsSpan()))
#endif
            {
                schemaFile = Path.GetFullPath(schemaFile);
            }

            result = schemaFile.Replace('\\', '/');
            return true;
        }

        result = null;
        return false;

#if NETSTANDARD2_0
        // <licensing>
        // Licensed to the .NET Foundation under one or more agreements.
        // The .NET Foundation licenses this file to you under the MIT license.
        // </licensing>
        static bool IsPartiallyQualified(ReadOnlySpan<char> path)
        {
            if (path.Length < 2)
            {
                // It isn't fixed, it must be relative.  There is no way to specify a fixed
                // path with one character (or less).
                return true;
            }

            if (IsDirectorySeparator(path[0]))
            {
                // There is no valid way to specify a relative path with two initial slashes or
                // \? as ? isn't valid for drive relative paths and \??\ is equivalent to \\?\
                return !(path[1] == '?' || IsDirectorySeparator(path[1]));
            }

            // The only way to specify a fixed path that doesn't begin with two slashes
            // is the drive, colon, slash format- i.e. C:\
            return !((path.Length >= 3)
                && (path[1] == Path.VolumeSeparatorChar)
                && IsDirectorySeparator(path[2])

                // To match old behavior we'll check the drive character for validity as the path is technically
                // not qualified if you don't have a valid drive. "=:\" is the "=" file's default data stream.
                && IsValidDriveChar(path[0]));
        }

        static bool IsDirectorySeparator(char c)
        {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }

        static bool IsValidDriveChar(char value)
        {
            return (uint)((value | 0x20) - 'a') <= (uint)('z' - 'a');
        }
#endif
    }
}