// <copyright file="IChildValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A child validation handler for a <see cref="IKeywordValidationHandler"/>.
/// </summary>
public interface IChildValidationHandler : IValidationHandler
{
    /// <summary>
    /// Appends code for this validation handler.
    /// </summary>
    /// <param name="generator">The code generator to which to append the validation method.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the validation method.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    /// <remarks>
    /// This method will be injected before the parent's code if its <see cref="IValidationHandler.ValidationHandlerPriority"/>
    /// is less than or equal to the parent's <see cref="IValidationHandler.ValidationHandlerPriority"/>,
    /// and afterwards if it is greater than that of the parent.
    /// </remarks>
    CodeGenerator AppendValidationCode(
        CodeGenerator generator,
        TypeDeclaration typeDeclaration);

    /// <summary>
    /// Appends any setup code required by the handler type. This is performed once at
    /// the start of the validation process.
    /// </summary>
    /// <param name="generator">The code generator to which to append the validation method.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the validation method.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    /// <remarks>
    /// This method will be injected before the parent's <see cref="IMethodBasedKeywordValidationHandlerWithChildren.AppendValidationSetup(CodeGenerator, TypeDeclaration)"/>
    /// code if its <see cref="IValidationHandler.ValidationHandlerPriority"/>
    /// is less than or equal to the parent's <see cref="IValidationHandler.ValidationHandlerPriority"/>,
    /// and afterwards if it is greater than that of the parent.
    /// </remarks>
    CodeGenerator AppendValidationSetup(
        CodeGenerator generator,
        TypeDeclaration typeDeclaration);

    /// <summary>
    /// Appends any setup code required by the handler type. This is performed once at
    /// the start of the validation method call, after determining whether the validation is ignored
    /// for type incompatibility.
    /// </summary>
    /// <param name="generator">The code generator to which to append the validation method.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the validation method.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    CodeGenerator AppendValidateMethodSetup(
        CodeGenerator generator,
        TypeDeclaration typeDeclaration);
}