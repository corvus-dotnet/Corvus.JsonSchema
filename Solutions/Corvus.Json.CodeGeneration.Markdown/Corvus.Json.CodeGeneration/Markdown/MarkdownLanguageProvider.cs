// <copyright file="MarkdownLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.Markdown;

/// <summary>
/// The Markdown language provider.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="MarkdownLanguageProvider"/> generates markdown-based documentation from a JSON Schema
/// file.
/// </para>
/// <para>
/// It uses the type analysis to provide information about the implied semantic patterns (e.g. strongly typed
/// maps, collections, tuples etc.) in addition to the basic keyword information.
/// </para>
/// </remarks>
public sealed class MarkdownLanguageProvider(MarkdownLanguageProvider.Options? options = null) : ILanguageProvider
{
    private readonly CodeFileBuilderRegistry codeFileBuilderRegistry = new();
    private readonly Options options = options ?? Options.Default;

    /// <summary>
    /// Gets the default <see cref="MarkdownLanguageProvider"/> instance.
    /// </summary>
    public static MarkdownLanguageProvider Default { get; } = CreateDefaultMarkdownProvider(null);

    /// <summary>
    /// Gets a <see cref="MarkdownLanguageProvider"/> instance with the default configuration and specified options.
    /// </summary>
    /// <param name="options">The options to set.</param>
    /// <returns>An instance of a <see cref="MarkdownLanguageProvider"/> with the default configuration and specified options.</returns>
    public static MarkdownLanguageProvider DefaultWithOptions(MarkdownLanguageProvider.Options options)
    {
        return CreateDefaultMarkdownProvider(options);
    }

    /// <inheritdoc/>
    public ILanguageProvider RegisterCodeFileBuilders(params ICodeFileBuilder[] builders)
    {
        this.codeFileBuilderRegistry.RegisterCodeFileBuilders(builders);
        return this;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<GeneratedCodeFile> GenerateCodeFor(IEnumerable<TypeDeclaration> typeDeclarations, CancellationToken cancellationToken)
    {
        HashSet<string> existingNames = [];
        CodeGenerator generator = new(this, cancellationToken);

        foreach (TypeDeclaration typeDeclaration in typeDeclarations)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return [];
            }

            typeDeclaration.SetOptions(this.options);

            if (generator.TryBeginTypeDeclaration(typeDeclaration))
            {
                foreach (ICodeFileBuilder codeFileBuilder in this.codeFileBuilderRegistry.RegisteredBuilders)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return [];
                    }

                    codeFileBuilder.EmitFile(generator, typeDeclaration);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return [];
                }

                generator.EndTypeDeclaration(typeDeclaration);
            }
        }

        return generator.GetGeneratedCodeFiles(t => new(t.Name(existingNames), this.options.FileExtension));
    }

    /// <inheritdoc/>
    public bool ShouldGenerate(TypeDeclaration typeDeclaration)
    {
        return true;
    }

    /// <inheritdoc/>
    public void IdentifyNonGeneratedType(TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        // NOP
    }

    private static MarkdownLanguageProvider CreateDefaultMarkdownProvider(Options? options)
    {
        MarkdownLanguageProvider languageProvider = new(options);

        languageProvider.RegisterCodeFileBuilders(
<<<<<<< HEAD
            MarkdownFileBuilder.Instance);
=======
            TypePartial.Instance);
>>>>>>> WIP - markdown language provider.

        return languageProvider;
    }

    /// <summary>
    /// Options for the <see cref="MarkdownLanguageProvider"/>.
    /// </summary>
    /// <param name="additionalSchemaDocumentation">The additional schema documentation.</param>
    /// <param name="fileExtension">Gets the file extension to use. Defaults to <c>.cs</c>.</param>
    public class Options(
        AdditionalSchemaDocumentation? additionalSchemaDocumentation = null,
        string fileExtension = ".md")
    {
        /// <summary>
        /// Gets the default options.
        /// </summary>
        public static Options Default { get; } = new();

        /// <summary>
        /// Gets the additional schema documentation.
        /// </summary>
        public AdditionalSchemaDocumentation? AdditionalSchemaDocumentation { get; } = additionalSchemaDocumentation;

        /// <summary>
        /// Gets the file extension (including the leading '.').
        /// </summary>
        internal string FileExtension { get; } = fileExtension;
    }
}