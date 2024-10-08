// <copyright file="ICodeFileBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Implemented by types that build a code file for a <see cref="TypeDeclaration"/>.
/// </summary>
/// <remarks>
/// Each code file builder will typically emit a single file for the <see cref="TypeDeclaration"/>
/// into the shared <see cref="CodeGenerator"/> for that type declaration.
/// </remarks>
public interface ICodeFileBuilder
{
    /// <summary>
    /// Emit a file for the type declaration.
    /// </summary>
    /// <param name="generator">The <see cref="CodeGenerator"/>.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    CodeGenerator EmitFile(CodeGenerator generator, TypeDeclaration typeDeclaration);
}