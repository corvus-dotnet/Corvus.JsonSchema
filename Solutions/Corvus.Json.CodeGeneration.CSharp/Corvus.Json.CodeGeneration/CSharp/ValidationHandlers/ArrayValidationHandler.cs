// <copyright file="ArrayValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A validation handler for the <see cref="IArrayValidationKeyword"/> capability.
/// </summary>
public class ArrayValidationHandler : KeywordValidationHandlerBase
{
    /// <summary>
    /// Gets a singleton instance of the <see cref="ArrayValidationHandler"/>.
    /// </summary>
    public static ArrayValidationHandler Instance { get; } = CreateDefault();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.AfterComposition;

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
            .AppendArrayValidation(generator.ValidationHandlerMethodName(this), typeDeclaration, this.ChildHandlers, this.ValidationHandlerPriority);
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
    public override bool HandlesKeyword(IKeyword keyword) => keyword is IArrayValidationKeyword;

    private static ArrayValidationHandler CreateDefault()
    {
        var result = new ArrayValidationHandler();
        result
            .RegisterChildHandlers(
                ArrayLengthValidationHandler.Instance,
                UniqueItemsValidationHandler.Instance);
        return result;
    }
}