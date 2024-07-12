// <copyright file="TernaryIfValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A validation handler for the <see cref="ITernaryIfValidationKeyword"/> capability.
/// </summary>
public class TernaryIfValidationHandler : KeywordValidationHandlerBase
{
    /// <summary>
    /// Gets a singleton instance of the <see cref="TernaryIfValidationHandler"/>.
    /// </summary>
    public static TernaryIfValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.Composition;

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationMethod(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationMethodCall(
        CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public override bool HandlesKeyword(IKeyword keyword) => keyword is ITernaryIfValidationKeyword;
}