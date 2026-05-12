// <copyright file="IChildArrayItemValidationHandler2.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ArrayChildHandlers;

internal interface IChildArrayItemValidationHandler2 : IChildArrayItemValidationHandler
{
    /// <summary>
    /// Indicates whether the array item handler will emit code for the given type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to check.</param>
    /// <returns><see langword="true"/> if this handler will emit code for the given type declaration; otherwise, <see langword="false"/>.</returns>
    bool WillEmitCodeFor(TypeDeclaration typeDeclaration);

    /// <summary>
    /// Gets the priority for the item handler, as opposed to the outer
    /// validation handler.
    /// </summary>
    /// <value>The priority value for this item handler.</value>
    uint ItemHandlerPriority { get; }
}