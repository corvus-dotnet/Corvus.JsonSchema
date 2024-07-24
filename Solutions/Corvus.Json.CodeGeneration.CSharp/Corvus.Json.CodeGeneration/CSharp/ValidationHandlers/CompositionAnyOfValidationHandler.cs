// <copyright file="CompositionAnyOfValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A validation handler for the <see cref="IAnyOfValidationKeyword"/> capability.
/// </summary>
public class CompositionAnyOfValidationHandler : KeywordValidationHandlerBase
{
    /// <summary>
    /// Gets a singleton instance of the <see cref="CompositionAnyOfValidationHandler"/>.
    /// </summary>
    public static CompositionAnyOfValidationHandler Instance { get; } = CreateDefault();

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
        return generator
            .AppendCompositionAnyOfValidation(generator.ValidationHandlerMethodName(this), typeDeclaration, this.ChildHandlers);
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
                ["this", generator.ResultIdentifierName(), generator.LevelIdentifierName()]);
    }

    /// <inheritdoc/>
    public override bool HandlesKeyword(IKeyword keyword) => keyword is IAnyOfValidationKeyword;

    private static CompositionAnyOfValidationHandler CreateDefault()
    {
        var result = new CompositionAnyOfValidationHandler();
        result
            .RegisterChildHandlers(
                AnyOfSubschemaValidationHandler.Instance);

        return result;
    }
}