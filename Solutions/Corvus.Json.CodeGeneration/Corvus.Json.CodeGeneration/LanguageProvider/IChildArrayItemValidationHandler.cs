// <copyright file="IChildArrayItemValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A child validation handler for an <see cref="IValidationHandler"/> that deals with
/// arrays and their items.
/// </summary>
public interface IChildArrayItemValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Append validation code for an individual array item.
    /// </summary>
    /// <param name="generator">The code generator to which to append the validation code.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the validation code.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    /// <remarks>
    /// The following additional variables will be in scope when this is called (in addition to those normally available from
    /// the parent handler).
    /// <para>
    /// <c>int length</c> - the current index in the array.
    /// <c>JsonArrayEnumerator arrayEnumerator</c> - the array enumerator (for non-strongly-typed arrays).
    /// <c>JsonArrayEnumerator&lt;TItem&gt; arrayEnumerator</c> - the array enumerator (for strongly-typed arrays).
    /// </para>
    /// </remarks>
    CodeGenerator AppendArrayItemValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration);
}