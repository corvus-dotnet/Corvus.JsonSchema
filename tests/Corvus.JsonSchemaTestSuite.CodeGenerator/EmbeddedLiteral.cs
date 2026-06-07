// <copyright file="EmbeddedLiteral.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.JsonSchemaTestSuite.CodeGenerator;

/// <summary>
/// Normalises values that are embedded verbatim into the generated test fixtures so the
/// emitted source is byte-identical regardless of the host operating system.
/// </summary>
internal static class EmbeddedLiteral
{
    /// <summary>
    /// Normalises embedded JSON text to LF line endings. The JSON Schema Test Suite files
    /// are checked out with CRLF on Windows and LF on Linux; embedding their raw text
    /// verbatim would otherwise make the generated fixtures differ per operating system.
    /// </summary>
    /// <param name="json">The raw JSON text.</param>
    /// <returns>The JSON text with all line endings normalised to LF.</returns>
    public static string Json(string json) => json.Replace("\r\n", "\n").Replace("\r", "\n");

    /// <summary>
    /// Normalises an embedded virtual file path to forward slashes. The path is a schema
    /// reference that the runtime helpers normalise to forward slashes regardless, so this
    /// only affects the generated source text, keeping it OS-independent.
    /// </summary>
    /// <param name="path">The path, possibly using the host OS directory separator.</param>
    /// <returns>The path with forward slashes.</returns>
    public static string ForwardSlash(string path) => path.Replace('\\', '/');
}