// <copyright file="IFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

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
    /// Append format-specific value getters to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the value getters.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the value getters.</param>
    /// <param name="format">The format for which to append the value getters.</param>
    /// <param name="seenValueGetters">Value getters that have already been generated, identified by a unique string.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatValueGetters(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenValueGetters);

    /// <summary>
    /// Append format-specific conversion operators to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the conversion operators.</param>
    /// <param name="typeDeclaration">The type declaration for which to append conversion operators.</param>
    /// <param name="format">The format for which to append conversion operators.</param>
    /// <param name="seenValueGetters">Conversion operators that have already been generated, identified by a unique string.</param>
    /// <param name="forMutable">If <see langword="true"/>, the code should be emitted for a mutable type.</param>
    /// <param name="useExplicit">If <see langword="true"/>, all operators will be emitted as <c>explicit</c>;
    /// otherwise the handler chooses <c>implicit</c> or <c>explicit</c> per format.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatConversionOperators(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenConversionOperators, bool forMutable, bool useExplicit = false);

    /// <summary>
    /// Append format-specific constructors for the <c>Source</c> to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the conversion operators.</param>
    /// <param name="typeDeclaration">The type declaration for which to append conversion operators.</param>
    /// <param name="format">The format for which to append conversion operators.</param>
    /// <param name="seenConstructorParameters">Constructors that have already been generated, identified by a unique string.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    /// <remarks>
    /// Typically, the <paramref name="seenConstructorParameters"/> would be a space separated list of the appropriately
    /// qualified names of the parameter types.
    /// </remarks>
    bool AppendFormatSourceConstructors(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenConstructorParameters);

    /// <summary>
    /// Append format-specific conversion operators for the <c>Source</c> to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the conversion operators.</param>
    /// <param name="typeDeclaration">The type declaration for which to append conversion operators.</param>
    /// <param name="format">The format for which to append conversion operators.</param>
    /// <param name="seenConversionOperators">Conversion operators that have already been generated, identified by a unique string.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    bool AppendFormatSourceConversionOperators(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenConversionOperators);

    /// <summary>
    /// Gets the expected <see cref="JsonTokenType"/> for instances
    /// that support the given format.
    /// </summary>
    /// <param name="format">The format for which to get the value kind.</param>
    /// <returns>The expected <see cref="JsonTokenType"/>, or <see langword="null"/>
    /// if the format was not handled by this instance.</returns>
    JsonTokenType? GetExpectedTokenType(string format);

    /// <summary>
    /// Tries to get the PascalCase suffix for a global simple type name
    /// for the given format (e.g., <c>"uuid"</c> → <c>"Uuid"</c>,
    /// <c>"int32"</c> → <c>"Int32"</c>).
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="suffix">When this method returns <see langword="true"/>, contains the
    /// PascalCase suffix to use after <c>Json</c> in the type name.</param>
    /// <returns><see langword="true"/> if this handler recognizes the format and can provide
    /// a simple type name suffix; otherwise, <see langword="false"/>.</returns>
    bool TryGetSimpleTypeNameSuffix(string format, [NotNullWhen(true)] out string? suffix);
}