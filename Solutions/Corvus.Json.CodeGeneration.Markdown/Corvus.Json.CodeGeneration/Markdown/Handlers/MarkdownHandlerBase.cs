// <copyright file="MarkdownHandlerBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Json.CodeGeneration.Markdown.Handlers;

/// <summary>
/// A base class for <see cref="IMarkdownHandler"/> implementations that support
/// child handlers.
/// </summary>
/// <typeparam name="T">The type implementing the base class.</typeparam>
/// <remarks>
/// <para>
/// You register child handlers with the <see cref="RegisterChildHandlers(Corvus.Json.CodeGeneration.Markdown.IMarkdownHandler[])"/>
/// method.
/// </para>
/// <para>Children whose <see cref="IMarkdownHandler.HandlerPriority"/> is less than or equal to <see cref="HandlerPriorities.Default"/>
/// will be called before <see cref="AppendMarkdownCore(Corvus.Json.CodeGeneration.CodeGenerator, Corvus.Json.CodeGeneration.TypeDeclaration, out ImmutableHashSet{Corvus.Json.CodeGeneration.IKeyword})"/>,
/// and those whose <see cref="IMarkdownHandler.HandlerPriority"/> is greater than <see cref="HandlerPriorities.Default"/> wil
/// be called afterwards.
/// </para>
/// </remarks>
public abstract class MarkdownHandlerBase<T> : IMarkdownHandler
    where T : notnull, MarkdownHandlerBase<T>
{
    private readonly MarkdownHandlerRegistry childRegistry = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownHandlerBase{T}"/> class.
    /// </summary>
    protected MarkdownHandlerBase()
    {
    }

    /// <inheritdoc/>
    public abstract uint HandlerPriority { get; }

    /// <summary>
    /// Registers markdown handlers with the parent handler.
    /// </summary>
    /// <param name="handlers">The handlers to register.</param>
    /// <returns>A reference to the handler once the operation has completed.</returns>
    public T RegisterChildHandlers(params IMarkdownHandler[] handlers)
    {
        this.childRegistry.RegisterMarkdownHandlers(handlers);
        return (T)this;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendMarkdown(CodeGenerator generator, TypeDeclaration typeDeclaration, out ImmutableHashSet<IKeyword> visitedKeywords)
    {
        HashSet<IKeyword> keywords = [];

        HandleChildren(
            generator,
            typeDeclaration,
            keywords,
            this.childRegistry.RegisteredHandlers.Where(r => r.HandlerPriority <= HandlerPriorities.Default));

        this.AppendMarkdownCore(generator, typeDeclaration, out ImmutableHashSet<IKeyword> ourKeywords);

        foreach (IKeyword childKeyword in ourKeywords)
        {
            keywords.Add(childKeyword);
        }

        HandleChildren(
            generator,
            typeDeclaration,
            keywords,
            this.childRegistry.RegisteredHandlers.Where(r => r.HandlerPriority > HandlerPriorities.Default));

        visitedKeywords = keywords.ToImmutableHashSet();

        return generator;

        static void HandleChildren(
            CodeGenerator generator,
            TypeDeclaration typeDeclaration,
            HashSet<IKeyword> keywords,
            IEnumerable<IMarkdownHandler> children)
        {
            foreach (IMarkdownHandler child in children)
            {
                child.AppendMarkdown(generator, typeDeclaration, out ImmutableHashSet<IKeyword> prependChildKeywords);
                foreach (IKeyword childKeyword in prependChildKeywords)
                {
                    keywords.Add(childKeyword);
                }
            }
        }
    }

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
    /// <para>
    /// <see cref="MarkdownHandlerBase{T}"/> ensures that the children whose <see cref="HandlerPriority"/> is less than or equal to the current handler's
    /// are appended before our own handler, and those with a <see cref="HandlerPriority"/> greater than the current handler's are appended after.
    /// </para>
    /// <para>
    /// This method is called between the prepend- and append- steps to add our own content.
    /// </para>
    /// <para>
    /// You should not call the base implementation of this method in your derived class.
    /// </para>
    /// </remarks>
    protected virtual CodeGenerator AppendMarkdownCore(CodeGenerator generator, TypeDeclaration typeDeclaration, out ImmutableHashSet<IKeyword> visitedKeywords)
    {
        visitedKeywords = ImmutableHashSet<IKeyword>.Empty;
        return generator;
    }
}