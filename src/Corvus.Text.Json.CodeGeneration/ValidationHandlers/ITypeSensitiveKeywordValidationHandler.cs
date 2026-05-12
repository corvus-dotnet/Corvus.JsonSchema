// <copyright file="ITypeSensitiveKeywordValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers;

/// <summary>
/// A validation handler for keywords that are sensitive to the Core type of the instance
/// they are validating.
/// </summary>
internal interface ITypeSensitiveKeywordValidationHandler : ITypeInsensitiveKeywordValidationHandler
{
    /// <summary>
    /// Append the validation code for the keyword.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration containing the keyword.</param>
    /// <param name="validateOnly">If <see langword="true"/>, then only the validation code should be emitted. Otherwise
    /// the wrapper to check the type of the outer element, the validation code, and the ignore code should be emitted.</param>
    /// <returns>The code generator, after the operation has completed.</returns>
    CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration, bool validateOnly);
}

/// <summary>
/// A validation handler for keywords that are not sensitive to the Core type of the instance
/// they are validating.
/// </summary>
internal interface ITypeInsensitiveKeywordValidationHandler : IKeywordValidationHandler
{
    /// <summary>
    /// Append the validation code for the keyword.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration containing the keyword.</param>
    /// <returns>The code generator, after the operation has completed.</returns>
    CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration);
}

/// <summary>
/// A validation handler for number-related keywords that are sensitive to the Core type of the instance
/// they are validating.
/// </summary>
internal interface INumberKeywordValidationHandler : ITypeSensitiveKeywordValidationHandler;

/// <summary>
/// A validation handler for string-related keywords that are sensitive to the Core type of the instance
/// they are validating.
/// </summary>
internal interface IStringKeywordValidationHandler : ITypeSensitiveKeywordValidationHandler;

/// <summary>
/// A validation handler for boolean-related keywords that are sensitive to the Core type of the instance
/// they are validating.
/// </summary>
internal interface IBooleanKeywordValidationHandler : ITypeSensitiveKeywordValidationHandler;

/// <summary>
/// A validation handler for null-related keywords that are sensitive to the Core type of the instance
/// they are validating.
/// </summary>
internal interface INullKeywordValidationHandler : ITypeSensitiveKeywordValidationHandler;

/// <summary>
/// A validation handler for object-related keywords that are sensitive to the Core type of the instance
/// they are validating.
/// </summary>
internal interface IObjectKeywordValidationHandler : ITypeSensitiveKeywordValidationHandler;

/// <summary>
/// A validation handler for array-related keywords that are sensitive to the Core type of the instance
/// they are validating.
/// </summary>
internal interface IArrayKeywordValidationHandler : ITypeSensitiveKeywordValidationHandler;

/// <summary>
/// Implemnted by validation handlers that deal with format for strings and numbers.
/// </summary>
internal interface IFormatKeywordValidationHandler : IKeywordValidationHandler
{
    /// <summary>
    /// Determines if the handler will generate code for the given core types.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="coreTypes">The core types.</param>
    /// <returns><see langword="true"/> if the format handler will generate code for the given core types.</returns>
    bool HandlesCoreTypes(TypeDeclaration typeDeclaration, CoreTypes coreTypes);

    /// <summary>
    /// Append the validation code for the keyword.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration containing the keyword.</param>
    /// <param name="validateOnly">If <see langword="true"/>, then only the validation code should be emitted. Otherwise
    /// the wrapper to check the type of the outer element, the validation code, and the ignore code should be emitted.</param>
    CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration, bool validateOnly = false);
}