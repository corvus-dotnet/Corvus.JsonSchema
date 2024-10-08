// <copyright file="GeneratedCodeFile.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// The results of generating code.
/// </summary>
/// <param name="typeDeclaration">The type declaration for which code was generated.</param>
/// <param name="fileName">The file name for the generated code.</param>
/// <param name="fileContent">The generated code.</param>
[DebuggerDisplay("{FileName}")]
public class GeneratedCodeFile(TypeDeclaration typeDeclaration, string fileName, string fileContent)
{
    /// <summary>
    /// Gets the type declaration for which code was generated.
    /// </summary>
    public TypeDeclaration TypeDeclaration { get; } = typeDeclaration;

    /// <summary>
    /// Gets the filename for the generated code.
    /// </summary>
    public string FileName { get; } = fileName;

    /// <summary>
    /// Gets the generated code itself.
    /// </summary>
    public string FileContent { get; } = fileContent;
}