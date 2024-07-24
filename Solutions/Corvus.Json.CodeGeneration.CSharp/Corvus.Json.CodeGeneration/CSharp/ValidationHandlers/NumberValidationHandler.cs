// <copyright file="NumberValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A validation handler for the <see cref="INumberValidationKeyword"/> capability.
/// </summary>
public class NumberValidationHandler : KeywordValidationHandlerBase
{
    /// <summary>
    /// Gets a singleton instance of the <see cref="NumberValidationHandler"/>.
    /// </summary>
    public static NumberValidationHandler Instance { get; } = CreateDefault();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.Default;

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
            .AppendNumberValidation(generator.ValidationHandlerMethodName(this), typeDeclaration, this.ChildHandlers);
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
    public override bool HandlesKeyword(IKeyword keyword) => keyword is INumberValidationKeyword;

    private static NumberValidationHandler CreateDefault()
    {
        var result = new NumberValidationHandler();
        result
            .RegisterChildHandlers(
                NumberRangeValidationHandler.Instance);
        return result;
    }
}