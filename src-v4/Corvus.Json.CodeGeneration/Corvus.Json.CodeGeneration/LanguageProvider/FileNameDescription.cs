// <copyright file="FileNameDescription.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Describes a base filename, extension, and separator.
/// </summary>
/// <param name="baseFileName">The base file name.</param>
/// <param name="extension">The file extension (or null if no extension is required).</param>
/// <param name="separator">The separator character between file name components (defaults to <c>'.'</c>).</param>
public readonly struct FileNameDescription(string baseFileName, string? extension, char separator = '.')
{
    /// <summary>
    /// Gets the base file name.
    /// </summary>
    public string BaseFileName { get; } = baseFileName;

    /// <summary>
    /// Gets the file extension, including any appropriate separator (or null if no extension is required).
    /// </summary>
    /// <remarks>
    /// e.g. <c>".cs"</c>.
    /// </remarks>
    public string? Extension { get; } = extension;

    /// <summary>
    /// Gets the separator character between file name components (e.g. <c>'.'</c> or <c>'_'</c>).
    /// </summary>
    public char Separator { get; } = separator;
}