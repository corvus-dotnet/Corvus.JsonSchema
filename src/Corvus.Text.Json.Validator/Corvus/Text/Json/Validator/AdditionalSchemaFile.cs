// <copyright file="AdditionalSchemaFile.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Text.Json.Validator;

/// <summary>
/// Specifies an additional schema file to preload into the document resolver.
/// </summary>
/// <param name="canonicalUri">The canonical URI for the schema.</param>
/// <param name="filePath">The file path to load the schema from.</param>
public sealed class AdditionalSchemaFile(string canonicalUri, string filePath)
{
    /// <summary>
    /// Gets the canonical URI for the schema.
    /// </summary>
    public string CanonicalUri { get; } = canonicalUri;

    /// <summary>
    /// Gets the file path to load the schema from.
    /// </summary>
    public string FilePath { get; } = filePath;
}