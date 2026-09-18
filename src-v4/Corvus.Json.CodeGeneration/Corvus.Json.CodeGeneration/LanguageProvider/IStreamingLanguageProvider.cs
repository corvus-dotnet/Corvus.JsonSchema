// <copyright file="IStreamingLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A language provider that can hand each generated file to a sink as it is completed, instead of returning the
/// whole collection at the end.
/// </summary>
public interface IStreamingLanguageProvider : ILanguageProvider
{
    /// <summary>
    /// Generates code for the type declarations, handing each file to the sink as it is completed.
    /// </summary>
    /// <param name="typeDeclarations">The type declarations for which to generate code.</param>
    /// <param name="sink">The sink that receives each file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    void GenerateCodeFor(IEnumerable<TypeDeclaration> typeDeclarations, IGeneratedCodeFileSink sink, CancellationToken cancellationToken);
}