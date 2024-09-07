// <copyright file="HeaderHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.Markdown.Handlers;

/// <summary>
/// An <see cref="IMarkdownHandler"/> for the header section of the documentation.
/// </summary>
public sealed class HeaderHandler : IMarkdownHandler
{
    private readonly MarkdownHandlerRegistry childRegistry = new();

    private HeaderHandler()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the <see cref="HeaderHandler"/>.
    /// </summary>
    public static HeaderHandler Instance { get; } = new();

    /// <inheritdoc/>
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

        return generator;
    }
}