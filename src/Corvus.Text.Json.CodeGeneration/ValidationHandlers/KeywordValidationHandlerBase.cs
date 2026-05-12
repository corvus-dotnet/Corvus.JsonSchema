// <copyright file="KeywordValidationHandlerBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers;

internal abstract class TypeSensitiveKeywordValidationHandlerBase : KeywordValidationHandlerBase, ITypeSensitiveKeywordValidationHandler
{
    /// <inheritdoc/>
    public abstract CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration, bool validateOnly);

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration) => AppendValidationCode(generator, typeDeclaration, validateOnly: false);
}

internal abstract class KeywordValidationHandlerBase : ITypeInsensitiveKeywordValidationHandler
{
    private readonly ChildValidationHandlerRegistry _childHandlers = new();

    /// <inheritdoc/>
    public abstract uint ValidationHandlerPriority { get; }

    /// <summary>
    /// Gets the collection of registered child handlers.
    /// </summary>
    protected IReadOnlyCollection<IChildValidationHandler> ChildHandlers => _childHandlers.RegisteredHandlers;

    /// <inheritdoc/>
    public abstract CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <inheritdoc/>
    public abstract bool HandlesKeyword(IKeyword keyword);

    /// <inheritdoc/>
    public abstract CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <summary>
    /// Registers child handlers for the validation handler type.
    /// </summary>
    /// <param name="children">The child handlers.</param>
    /// <returns>An instance of the parent <see cref="IKeywordValidationHandler"/> once the operation has completed.</returns>
    /// <remarks>
    /// The registered <see cref="IChildValidationHandler"/> will typically have their setup and validation injected
    /// either before or after the code emitted by <see cref="AppendValidationSetup(CodeGenerator, TypeDeclaration)"/>
    /// and <see cref="AppendValidationCode(CodeGenerator, TypeDeclaration)"/> (respectively), depending on their relative
    /// <see cref="IValidationHandler.ValidationHandlerPriority"/> with their parent.
    /// </remarks>
    /// <inheritdoc/>
    public IKeywordValidationHandler RegisterChildHandlers(params IChildValidationHandler[] children)
    {
        _childHandlers.RegisterValidationHandlers(children);
        return this;
    }
}