// <copyright file="TypePartial.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.Markdown;

/// <summary>
/// The code file builder for the markdown.
/// </summary>
public sealed class TypePartial : ICodeFileBuilder
{
    
    private TypePartial()
    {
    }

    /// <summary>
    /// Gets the default instance of the type partial.
    /// </summary>
    public static TypePartial Instance { get; } = new();

    /// <inheritdoc/>
    public CodeGenerator EmitFile(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendTitle(typeDeclaration)
            .AppendDocumentation(typeDeclaration)
            .AppendCoreTypes(typeDeclaration)
            .AppendAnalysis(typeDeclaration);
            ////.AppendKeywords(typeDeclaration)
            ////.AppendExamples(typeDeclaration);
    }
}