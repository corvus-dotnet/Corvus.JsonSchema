// <copyright file="ObjectValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A validation handler for the <see cref="IObjectValidationKeyword"/> capability.
/// </summary>
public class ObjectValidationHandler : KeywordValidationHandlerBase
{
    /// <summary>
    /// Gets a singleton instance of the <see cref="ObjectValidationHandler"/>.
    /// </summary>
    public static ObjectValidationHandler Instance { get; } = CreateDefault();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.AfterComposition;

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .PrependChildValidationSetup(typeDeclaration, this.ChildHandlers, this.ValidationHandlerPriority)
            .PushJsonPropertyNamesClassNameAndScope()
            .AppendChildValidationSetup(typeDeclaration, this.ChildHandlers, this.ValidationHandlerPriority);
    }

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationMethod(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendObjectValidation(generator.ValidationHandlerMethodName(this), typeDeclaration, this.ChildHandlers);
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
    public override bool HandlesKeyword(IKeyword keyword) => keyword is IObjectValidationKeyword;

    private static ObjectValidationHandler CreateDefault()
    {
        var result = new ObjectValidationHandler();
        result
            .RegisterChildHandlers(
                DependentSchemasValidationHandler.Instance,
                PropertyCountValidationHandler.Instance,
                PropertyNamesValidationHandler.Instance,
                PatternPropertiesValidationHandler.Instance,
                PropertiesValidationHandler.Instance,
                RequiredValidationHandler.Instance);
        return result;
    }
}