// <copyright file="IKeywordValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A validation handler that is capable of handling one or more keywords.
/// </summary>
public interface IKeywordValidationHandler : IValidationHandler
{
    /// <summary>
    /// Gets a value indicating whether the validation handler can handle
    /// the given keyword.
    /// </summary>
    /// <param name="keyword">The keyword being handled.</param>
    /// <returns><see langword="true"/> if the handler handles the keyword.</returns>
    bool HandlesKeyword(IKeyword keyword);

    /// <summary>
    /// Appends any setup code required by the handler type. This is performed once at
    /// the start of the validation process.
    /// </summary>
    /// <param name="generator">The code generator to which to append the setup code.</param>
    /// <param name="typeDeclaration">The type declaration for which to append setup code.</param>
    /// <returns>A reference to the builder after the operation has completed.</returns>
    /// <remarks>
    /// It is the responsibility of the handler implementation to ensure that it conforms with the
    /// specific <see cref="ILanguageProvider"/> requirements for orchestration of this call with the
    /// <see cref="AppendValidationMethod(CodeGenerator, TypeDeclaration)"/> and
    /// <see cref="AppendValidationMethodCall(CodeGenerator, TypeDeclaration)"/>.
    /// </remarks>
    CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <summary>
    /// Appends a support method implementing the validation code for this validation handler.
    /// </summary>
    /// <param name="generator">The code generator to which to append the validation method.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the validation method.</param>
    /// <returns>A reference to the builder after the operation has completed.</returns>
    /// <remarks>
    /// It is the responsibility of the handler implementation to ensure that it conforms with the
    /// specific <see cref="ILanguageProvider"/> requirements for orchestration of this call with the
    /// <see cref="AppendValidationMethodCall(CodeGenerator, TypeDeclaration)"/> and
    /// <see cref="AppendValidationSetup(CodeGenerator, TypeDeclaration)"/>.
    /// </remarks>
    CodeGenerator AppendValidationMethod(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <summary>
    /// Appends a call to the method implementing the validation code emitted by
    /// <see cref="AppendValidationMethod(CodeGenerator, TypeDeclaration)"/>.
    /// </summary>
    /// <param name="generator">The code generator to which to append the call to the validation method.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the call to the validation method.</param>
    /// <returns>A reference to the builder after the operation has completed.</returns>
    /// <remarks>
    /// It is the responsibility of the handler implementation to ensure that it conforms with the
    /// specific <see cref="ILanguageProvider"/> requirements for orchestration of this call with the
    /// <see cref="AppendValidationMethod(CodeGenerator, TypeDeclaration)"/> and
    /// <see cref="AppendValidationSetup(CodeGenerator, TypeDeclaration)"/>.
    /// </remarks>
    CodeGenerator AppendValidationMethodCall(
        CodeGenerator generator,
        TypeDeclaration typeDeclaration);

    /// <summary>
    /// Registers a child handler for the validation handler type.
    /// </summary>
    /// <param name="children">The child handlers.</param>
    /// <returns>An instance of the parent <see cref="IValidationHandler"/> once the operation has completed.</returns>
    /// <remarks>
    /// The registered <see cref="IChildValidationHandler"/> will typically have their setup and validation injected
    /// either before or after the code emitted by <see cref="AppendValidationSetup(CodeGenerator, TypeDeclaration)"/>
    /// and <see cref="AppendValidationMethod(CodeGenerator, TypeDeclaration)"/> (respectively), depending on their relative
    /// <see cref="IValidationHandler.ValidationHandlerPriority"/> with their parent.
    /// </remarks>
    IKeywordValidationHandler RegisterChildHandlers(params IChildValidationHandler[] children);
}