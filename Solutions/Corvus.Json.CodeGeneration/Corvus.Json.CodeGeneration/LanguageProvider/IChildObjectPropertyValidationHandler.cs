// <copyright file="IChildObjectPropertyValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A child validation handler for an <see cref="IValidationHandler"/> that deals with
/// arrays and their items.
/// </summary>
public interface IChildObjectPropertyValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Append validation code for an individual object property.
    /// </summary>
    /// <param name="generator">The code generator to which to append the validation code.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the validation code.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    /// <remarks>
    /// The following additional variables will be in scope when this is called (in addition to those normally available from
    /// the parent handler).
    /// <para>
    /// <c>int propertyCount</c> - the index of the property in the object enumeration.
    /// <c>JsonObjectEnumerator objectEnumerator</c> - the object enumerator (for non-map-like objects).
    /// <c>JsonObjectEnumerator&lt;TItem&gt; objectEnumerator</c> - the object enumerator (for map-like objects).
    /// <c>JsonObjectProperty property</c> - the current object property (for non-map-like objects).
    /// <c>JsonObjectProperty&lt;TItem&gt; property</c> - the current object property (for map-like objects).
    /// <c>string propertyName</c> - the current property name, if any handlers determined that they require the property name as a string.
    /// </para>
    /// </remarks>
    CodeGenerator AppendObjectPropertyValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <summary>
    /// Gets a value indicating whether the handler requires property names as a string.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the handler requires property names as a string.</returns>
    bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration);
}