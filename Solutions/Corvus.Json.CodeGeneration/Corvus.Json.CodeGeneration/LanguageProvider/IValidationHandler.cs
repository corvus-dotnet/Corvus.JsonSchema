// <copyright file="IValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Handles validation for a particular type of validator.
/// </summary>
/// <remarks>
/// <para>
/// Handlers for specific keywords implement <see cref="IKeywordValidationHandler"/> and are registered with a <see cref="KeywordValidationHandlerRegistry"/> which
/// a <see cref="ILanguageProvider"/> instance uses to generate validation code for
/// a <see cref="TypeDeclaration"/>.
/// </para>
/// <para>
/// While most <see cref="ILanguageProvider"/> implementations will register a standard set of <see cref="IValidationHandler"/>
/// instances, using custom vocabularies may require you to add new <see cref="IKeywordValidationHandler"/> or child <see cref="IValidationHandler"/> instances to add custom validation capabilities.
/// </para>
/// </remarks>
public interface IValidationHandler
{
    /// <summary>
    /// Gets the relative priority for the <see cref="IValidationHandler"/>.
    /// </summary>
    uint ValidationHandlerPriority { get; }
}