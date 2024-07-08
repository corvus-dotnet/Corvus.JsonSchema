// <copyright file="KeywordValidationHandlerBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A base class for <see cref="IKeywordValidationHandler"/> implementations.
/// </summary>
public abstract class KeywordValidationHandlerBase : IKeywordValidationHandler
{
    private readonly ChildValidationHandlerRegistry childHandlers = new();

    /// <inheritdoc/>
    public abstract uint ValidationHandlerPriority { get; }

    /// <summary>
    /// Gets the child handlers.
    /// </summary>
    protected IReadOnlyCollection<IChildValidationHandler> ChildHandlers => this.childHandlers.RegisteredHandlers;

    /// <inheritdoc/>
    public abstract CodeGenerator AppendValidationMethod(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <inheritdoc/>
    public abstract CodeGenerator AppendValidationMethodCall(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <inheritdoc/>
    public abstract CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration);

    /// <inheritdoc/>
    public abstract bool HandlesKeyword(IKeyword keyword);

    /// <inheritdoc/>
    public IKeywordValidationHandler RegisterChildHandlers(params IChildValidationHandler[] children)
    {
        this.childHandlers.RegisterValidationHandlers(children);
        return this;
    }
}