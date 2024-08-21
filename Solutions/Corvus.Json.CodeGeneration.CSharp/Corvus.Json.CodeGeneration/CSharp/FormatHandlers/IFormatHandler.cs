// <copyright file="IFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Support for well-known format types.
/// </summary>
public interface IFormatHandler
{
    /// <summary>
    /// Gets the priority of the handler.
    /// </summary>
    /// <remarks>
    /// Handlers will be executed in priority order.
    /// </remarks>
    uint Priority { get; }

    /// <summary>
    /// Gets the .NET type name for the given candidate format (e.g. JsonUuid, JsonIri, JsonInt64 etc).
    /// </summary>
    /// <param name="format">The candidate format.</param>
    /// <returns>The corresponding .NET type name, or <see langword="null"/> if the format was not handled by this instance.</returns>
    string? GetCorvusJsonTypeNameFor(string format);

    /// <summary>
    /// Gets the expected <see cref="JsonValueKind"/> for instances
    /// that support the given format.
    /// </summary>
    /// <param name="format">The format for which to get the value kind.</param>
    /// <returns>The expected <see cref="JsonValueKind"/>, or <see langword="null"/>
    /// if the format was not handled by this instance.</returns>
    JsonValueKind? GetExpectedValueKind(string format);

    /// <summary>
    /// Append format-specific expressions in the body of an <c>Equals&lt;T&gt;</c> method where the comparison values are <c>this</c> and <c>other</c>.
    /// </summary>
    /// <param name="generator">The generator to which to append the format assertion.</param>
    /// <param name="typeDeclaration">The type declaration for which to append equals expressions.</param>
    /// <param name="format">The format for which to append constructors.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatEqualsTBody(CodeGenerator generator, TypeDeclaration typeDeclaration, string format);

    /// <summary>
    /// Append a format assertion to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the format assertion.</param>
    /// <param name="format">The format to assert.</param>
    /// <param name="valueIdentifier">The identifier for the value to test.</param>
    /// <param name="validationContextIdentifier">The identifier for the validation context to update.</param>
    /// <param name="includeType">If <see langword="true"/>, the type assertion is also included.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatAssertion(CodeGenerator generator, string format, string valueIdentifier, string validationContextIdentifier, bool includeType);

    /// <summary>
    /// Append format-specific constructors to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the format constructors.</param>
    /// <param name="typeDeclaration">The type declaration for which to append constructors.</param>
    /// <param name="format">The format for which to append constructors.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatConstructors(CodeGenerator generator, TypeDeclaration typeDeclaration, string format);

    /// <summary>
    /// Append format-specific public static properties to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the format properties.</param>
    /// <param name="typeDeclaration">The type declaration for which to append properties.</param>
    /// <param name="format">The format for which to append properties.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatPublicStaticProperties(CodeGenerator generator, TypeDeclaration typeDeclaration, string format);

    /// <summary>
    /// Append format-specific public properties to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the format properties.</param>
    /// <param name="typeDeclaration">The type declaration for which to append properties.</param>
    /// <param name="format">The format for which to append properties.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatPublicProperties(CodeGenerator generator, TypeDeclaration typeDeclaration, string format);

    /// <summary>
    /// Append format-specific conversion operators to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the conversion operators.</param>
    /// <param name="typeDeclaration">The type declaration for which to append conversion operators.</param>
    /// <param name="format">The format for which to append conversion operators.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatConversionOperators(CodeGenerator generator, TypeDeclaration typeDeclaration, string format);

    /// <summary>
    /// Append format-specific public static methods to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the format methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to append methods.</param>
    /// <param name="format">The format for which to append methods.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatPublicStaticMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format);

    /// <summary>
    /// Append format-specific public methods to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the format methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to append methods.</param>
    /// <param name="format">The format for which to append methods.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatPublicMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format);

    /// <summary>
    /// Append format-specific private static methods to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the format methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to append methods.</param>
    /// <param name="format">The format for which to append methods.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatPrivateStaticMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format);

    /// <summary>
    /// Append format-specific private methods to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the format methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to append methods.</param>
    /// <param name="format">The format for which to append methods.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatPrivateMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format);

    /// <summary>
    /// Append a format-specific constant value.
    /// </summary>
    /// <param name="generator">The generator to which to append the constant value.</param>
    /// <param name="keyword">The keyword producing the constant.</param>
    /// <param name="format">The format for which to append the constant value.</param>
    /// <param name="fieldName">The field name for the constant.</param>
    /// <param name="constantValue">The constant value as a <see cref="JsonElement"/>.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatConstant(CodeGenerator generator, ITypedValidationConstantProviderKeyword keyword, string format, string fieldName, JsonElement constantValue);
}