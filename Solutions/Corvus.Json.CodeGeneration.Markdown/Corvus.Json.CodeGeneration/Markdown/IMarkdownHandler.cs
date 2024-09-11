// <copyright file="IMarkdownHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

<<<<<<< HEAD
using System.Collections.Immutable;

=======
>>>>>>> ae8f75a5024a41962b6ac63aceabbb0388e32f52
namespace Corvus.Json.CodeGeneration.Markdown;

/// <summary>
/// Implemented by classes that can handle a specific JSON Schema capability in the markdown generation process.
/// </summary>
public interface IMarkdownHandler
{
    /// <summary>
    /// Gets the priority for the handler.
    /// </summary>
    /// <remarks>
    /// This allows you to control the ordering of the handlers. The handlers are called in order of priority, from the lowest to the highest.
    /// </remarks>
    uint HandlerPriority { get; }

    /// <summary>
    /// Handles the capability for the specified type declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the capability.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="visitedKeywords">A collection to which to add The keywords that have been interpreted by this handler.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// <para>
    /// The handler is intended to add the keywords it has documented to the <paramref name="visitedKeywords"/>
    /// collection. Unlike, for example, the <c>CSharpLanguageProvider</c>, we need to document all keywords and so the <see cref="MarkdownLanguageProvider"/>
    /// maintains a collection of all keywords that have been documented so far, and the outstanding keywords are documented at the end.
    /// </para>
    /// </remarks>
<<<<<<< HEAD
    CodeGenerator AppendMarkdown(CodeGenerator generator, TypeDeclaration typeDeclaration, out ImmutableHashSet<IKeyword> visitedKeywords);
=======
    CodeGenerator AppendMarkdown(CodeGenerator generator, TypeDeclaration typeDeclaration, out IReadOnlyCollection<IKeyword> visitedKeywords);
>>>>>>> ae8f75a5024a41962b6ac63aceabbb0388e32f52
}