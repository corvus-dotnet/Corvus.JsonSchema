// <copyright file="MarkdownFileBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Corvus.Json.CodeGeneration.Markdown.Handlers;

namespace Corvus.Json.CodeGeneration.Markdown;

/// <summary>
/// The code file builder for the markdown.
/// </summary>
public sealed class MarkdownFileBuilder : ICodeFileBuilder
{
    private MarkdownFileBuilder()
    {
    }

    /// <summary>
    /// Gets the default instance of the type partial.
    /// </summary>
    public static MarkdownFileBuilder Instance { get; } = CreateDefaultInstance();

    /// <inheritdoc/>
    public CodeGenerator EmitFile(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        MarkdownHandler.Instance.AppendMarkdown(generator, typeDeclaration, out ImmutableHashSet<IKeyword> keywords);

        this.AppendUnknownKeywords(typeDeclaration.Keywords().Where(k => !keywords.Contains(k)));

        return generator;
    }

    /// <summary>
    /// Registers markdown handlers with the parent handler.
    /// </summary>
    /// <param name="handlers">The handlers to register.</param>
    /// <returns>A reference to the handler once the operation has completed.</returns>
    public MarkdownFileBuilder RegisterMarkdownHandlers(params IMarkdownHandler[] handlers)
    {
        MarkdownHandler.Instance.RegisterChildHandlers(handlers);
        return this;
    }

    private static MarkdownFileBuilder CreateDefaultInstance()
    {
        return
            new MarkdownFileBuilder()
                .RegisterMarkdownHandlers(
                    HeaderHandler.Instance);
    }

    private void AppendUnknownKeywords(IEnumerable<IKeyword> unknownKeywords)
    {
        throw new NotImplementedException();
    }

    private sealed class MarkdownHandler : MarkdownHandlerBase<MarkdownHandler>
    {
        private MarkdownHandler()
        {
        }

        public static MarkdownHandler Instance { get; } = new();

        public override uint HandlerPriority => HandlerPriorities.Default;
    }
}