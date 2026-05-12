// <copyright file="INamedPropertyChildHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

/// <summary>
/// Implemented by types that will handle validation for JSON properties.
/// </summary>
/// <remarks>
/// This is used by <see cref="PropertiesValidationHandler"/> to emit the code
/// to validate a named property.
/// </remarks>
public interface INamedPropertyChildHandler
{
    /// <summary>
    /// Gets the validation handler priority.
    /// </summary>
    uint ValidationHandlerPriority { get; }

    /// <summary>
    /// Called at the start of JSON Schema class setup to emit code into the <c>JsonSchema</c> class.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit code.</param>
    void BeginJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <summary>
    /// Called between <see cref="BeginJsonSchemaClassSetup(CodeGenerator, TypeDeclaration)"/> and <see cref="EndJsonSchemaClassSetup(CodeGenerator, TypeDeclaration)"/>
    /// to emit code into the <c>JsonSchema</c> class for each individual property declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit code.</param>
    /// <param name="property">The property declaration for which to emit code.</param>
    /// <returns><see langword="true"/> if code was emitted for the property.</returns>
    bool AppendJsonSchemaClassSetupForProperty(CodeGenerator generator, TypeDeclaration typeDeclaration, PropertyDeclaration property);

    /// <summary>
    /// Called at the end of JSON Schema class setup to emit code into the <c>JsonSchema</c> class.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit code.</param>
    void EndJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <summary>
    /// Emits code into the specific property validator method for the specified property.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit code.</param>
    /// <param name="property">The property declaration for which to emit code.</param>
    /// <remarks>
    /// <para>
    /// This is called for each property declaration, and should emit code into the specific property validator method for the property if appropriate.
    /// </para>
    /// </remarks>
    void AppendObjectPropertyValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration, PropertyDeclaration property);

    /// <summary>
    /// Appends validation code for the properties validation method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit code.</param>
    /// <remarks>
    /// This emits code into the properties validation method, as opposed to the specific property validation method. As such, it is called once per type declaration.
    /// </remarks>
    void AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <summary>
    /// Appends validation setup code for the properties validation method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit code.</param>
    void AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <summary>
    /// Gets the parameters to be passed to the property validator for the specified type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the parameters.</param>
    /// <returns>The parameters to be passed to the property validator.</returns>
    IEnumerable<ObjectPropertyValidatorParameter> GetNamedPropertyValidatorParameters(TypeDeclaration typeDeclaration);

    /// <summary>
    /// Appends arguments to be passed to the property validator for the specified type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the arguments.</param>
    /// <remarks>
    /// The caller will ensure that appropriate commas are emitted after any arguments you add here, but
    /// you are responsible for *always* adding a preceding comma if you add any arguments.
    /// </remarks>
    void AppendValidatorArguments(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <summary>
    /// Indicates whether this handler will emit code for the given type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if code will be emitted for the property, otherwise <see langword="false"/>.</returns>
    bool WillEmitCodeFor(TypeDeclaration typeDeclaration);

    /// <summary>
    /// Gets a value indicating whether the validation handler evaluates the property if code is emitted for the property.
    /// </summary>
    /// <param name="property">The property to test.</param>
    /// <remarks>
    /// <para>
    /// Some validators (e.g. Dependent Schemas) do not actually validate the property for which their validation code is emitted, but instead use the presence of the property
    /// to trigger other validation. In such cases, this property should return <see langword="false"/> to indicate that the property is not actually evaluated by the validation
    /// handler, even though code is emitted for it.
    /// </para>
    /// </remarks>
    bool EvaluatesProperty(PropertyDeclaration property);
}