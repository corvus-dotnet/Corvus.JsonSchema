// <copyright file="CompositionOneOfValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A validation handler for the <see cref="IOneOfValidationKeyword"/> capability.
/// </summary>
public class CompositionOneOfValidationHandler : KeywordValidationHandlerBase
{
    /// <summary>
    /// Gets a singleton instance of the <see cref="CompositionOneOfValidationHandler"/>.
    /// </summary>
    public static CompositionOneOfValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.Composition;

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .PrependChildValidationSetup(typeDeclaration, this.ChildHandlers, this.ValidationHandlerPriority)
            .AppendChildValidationSetup(typeDeclaration, this.ChildHandlers, this.ValidationHandlerPriority);
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
        // This occurs in the parent context, so we need to add the validation class name to the scope.
        return generator
            .AppendValidationMethodCall(
                generator.ValidationClassName(),
                generator.ValidationHandlerMethodName(this),
                ["this", generator.ValueKindIdentifierName(), generator.ResultIdentifierName(), generator.LevelIdentifierName()]);
    }

    /// <inheritdoc/>
    public override bool HandlesKeyword(IKeyword keyword) => keyword is IOneOfValidationKeyword;
}