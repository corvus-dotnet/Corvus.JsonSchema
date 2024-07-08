// <copyright file="ILanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Provides code generation semantics for the output language.
/// </summary>
public interface ILanguageProvider
{
    /// <summary>
    /// Registers code file builders with the language provider.
    /// </summary>
    /// <param name="builders">The code file builders to register.</param>
    /// <returns>A reference to the <see cref="ILanguageProvider"/> instance after the operation has been completed.</returns>
    ILanguageProvider RegisterCodeFileBuilders(params ICodeFileBuilder[] builders);

    /// <summary>
    /// Registers keyword validation handlers with the language provider.
    /// </summary>
    /// <param name="handlers">The handlers to register.</param>
    /// <returns>A reference to the <see cref="ILanguageProvider"/> instance after the operation has been completed.</returns>
    ILanguageProvider RegisterValidationHandlers(params IKeywordValidationHandler[] handlers);

    /// <summary>
    /// Register name heuristics for the language provider.
    /// </summary>
    /// <param name="heuristics">the naming heuristics to register.</param>
    /// <returns>A reference to the <see cref="ILanguageProvider"/> instance after the operation has been completed.</returns>
    ILanguageProvider RegisterNameHeuristics(params INameHeuristic[] heuristics);

    /// <summary>
    /// Gets the registered validation handlers for the given keyword.
    /// </summary>
    /// <param name="keyword">The given keyword.</param>
    /// <param name="validationHandlers">The collection of <see cref="IValidationHandler"/> instances for the handler type, or
    /// <see langword="null"/> if no handler was registered.</param>
    /// <returns><see langword="true"/> if any handlers were found for the handler type.</returns>
    bool TryGetValidationHandlersFor(IKeyword keyword, [NotNullWhen(true)] out IReadOnlyCollection<IKeywordValidationHandler>? validationHandlers);

    /// <summary>
    /// Generates code for one or more type declarations.
    /// </summary>
    /// <param name="typeDeclarations">The type declarations for which to generate code.</param>
    /// <returns>The generated code files.</returns>
    /// <remarks>
    /// The type declarations passed to this method should be reduced, and ready to be generated.
    /// Typically, they will be provided by a call to <see cref="JsonSchemaTypeBuilder.GenerateCodeUsing(ILanguageProvider, TypeDeclaration[])"/>.
    /// </remarks>
    IReadOnlyCollection<GeneratedCodeFile> GenerateCodeFor(IEnumerable<TypeDeclaration> typeDeclarations);

    /// <summary>
    /// Gets a value indicating whether the type should be generated in this language.
    /// </summary>
    /// <param name="type">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type should be generated.</returns>
    /// <remarks>
    /// This is typically used to ignore built-in types provided by the language concerned.
    /// </remarks>
    bool ShouldGenerate(TypeDeclaration type);

    /// <summary>
    /// Set the name for the type declaration, before its subschema names have been set.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to set the name.</param>
    /// <param name="fallbackName">The name to use as a fallback for the type declaration.</param>
    void SetNameBeforeSubschema(TypeDeclaration typeDeclaration, string fallbackName);

    /// <summary>
    /// Set the name for the type declaration, after its subschema names have been set.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to set the name.</param>
    void SetNameAfterSubschema(TypeDeclaration typeDeclaration);
}