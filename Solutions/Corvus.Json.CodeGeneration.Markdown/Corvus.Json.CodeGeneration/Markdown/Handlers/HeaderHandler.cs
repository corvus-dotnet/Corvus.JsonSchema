// <copyright file="HeaderHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Json.CodeGeneration.Markdown.Handlers;

/// <summary>
/// An <see cref="IMarkdownHandler"/> for the header section of the documentation.
/// </summary>
public sealed class HeaderHandler : MarkdownHandlerBase<HeaderHandler>
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="HeaderHandler"/>.
    /// </summary>
    public static HeaderHandler Instance { get; } = new();

    /// <inheritdoc/>
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
        return generator;
    }
}