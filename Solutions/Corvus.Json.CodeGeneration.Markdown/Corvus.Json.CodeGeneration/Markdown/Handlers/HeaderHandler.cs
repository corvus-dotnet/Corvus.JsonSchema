// <copyright file="HeaderHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

<<<<<<< HEAD
using System.Collections.Immutable;

=======
>>>>>>> WIP - markdown language provider.
namespace Corvus.Json.CodeGeneration.Markdown.Handlers;

/// <summary>
/// An <see cref="IMarkdownHandler"/> for the header section of the documentation.
/// </summary>
<<<<<<< HEAD
public sealed class HeaderHandler : MarkdownHandlerBase<HeaderHandler>
{
=======
public sealed class HeaderHandler : IMarkdownHandler
{
    private readonly MarkdownHandlerRegistry childRegistry = new();

    private HeaderHandler()
    {
    }

>>>>>>> WIP - markdown language provider.
    /// <summary>
    /// Gets the singleton instance of the <see cref="HeaderHandler"/>.
    /// </summary>
    public static HeaderHandler Instance { get; } = new();

    /// <inheritdoc/>
<<<<<<< HEAD
    public override uint HandlerPriority => HandlerPriorities.Header;

    /// <inheritdoc/>
    protected override CodeGenerator AppendMarkdownCore(
        CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        out ImmutableHashSet<IKeyword> visitedKeywords)
    {
        generator
            .AppendTitle(typeDeclaration);

        visitedKeywords = ImmutableHashSet<IKeyword>.Empty;
=======
    public uint HandlerPriority => HandlerPriorities.Header;

    /// <inheritdoc/>
    public CodeGenerator AppendMarkdown(CodeGenerator generator, TypeDeclaration typeDeclaration, out IReadOnlyCollection<IKeyword> visitedKeywords)
    {
        HashSet<IKeyword> keywords = [];

        foreach (IMarkdownHandler child in this.childRegistry.RegisteredHandlers)
        {
            child.AppendMarkdown(generator, typeDeclaration, out IReadOnlyCollection<IKeyword> childKeywords);
            foreach (IKeyword childKeyword in childKeywords)
            {
                keywords.Add(childKeyword);
            }
        }

        visitedKeywords = keywords;

>>>>>>> WIP - markdown language provider.
        return generator;
    }
}