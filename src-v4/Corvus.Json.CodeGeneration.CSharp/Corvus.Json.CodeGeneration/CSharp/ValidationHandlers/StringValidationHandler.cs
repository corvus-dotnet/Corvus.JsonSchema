// <copyright file="StringValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A validation handler for <see cref="IStringValidationKeyword"/> capability.
/// </summary>
/// <remarks>
/// <para>
/// The child handler code is emitted in a context where the following values are in scope.
/// <code>
/// ReadOnlySpan&lt;char&gt; input;
/// int length;
/// in ValidationContextWrapper context;
/// out ValidationContext result;
/// </code>
/// </para>
/// <para>
/// The result should be updated with the result of validation.
/// </para>
/// <para>
/// Length is available only if a <see cref="IStringValidationKeyword"/> present on the type declaration
/// asserted <see cref="IStringValidationKeyword.RequiresStringLength"/> to be <see langword="true"/>.
/// </para>
/// </remarks>
public class StringValidationHandler : KeywordValidationHandlerBase
{
    /// <summary>
    /// Gets a singleton instance of the <see cref="StringValidationHandler"/>.
    /// </summary>
    public static StringValidationHandler Instance { get; } = CreateDefault();

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
            .AppendStringValidation(generator.ValidationHandlerMethodName(this), typeDeclaration, this.ChildHandlers);
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
    public override bool HandlesKeyword(IKeyword keyword) => keyword is IStringValidationKeyword;

    private static StringValidationHandler CreateDefault()
    {
        var result = new StringValidationHandler();
        result
            .RegisterChildHandlers(
                StringRegexValidationHandler.Instance,
                StringLengthValidationHandler.Instance);
        return result;
    }
}