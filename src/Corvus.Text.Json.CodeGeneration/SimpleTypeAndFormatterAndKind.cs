// <copyright file="SimpleTypeAndFormatterAndKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Gets the type name and formatter for the simple type.
/// </summary>
public readonly struct SimpleTypeAndFormatterAndKind
{
    /// <summary>
    /// The appropriately qualified .NET type name for the type.
    /// </summary>
    public readonly string DotnetTypeName;

    /// <summary>
    /// Gets the expression that will format an instance of the value type as a UTF-8 string.
    /// </summary>
    /// <remarks>
    /// The expression will be called in the context where the value is provided via and identifier
    /// <c>v</c>, the output <see cref="Span{T}"/> will be <c>buffer</c>, and the <see langword="out"/> parameter
    /// <c>written</c> will receive the number of bytes written. e.g. <c>Utf8Formatter.TryFormat(v, buffer, out written)</c>
    /// </remarks>
    public readonly string FormatterExpression;

    /// <summary>
    /// Gets the expression that specifies the <c>Kind</c> to apply when the value is formatted.
    /// </summary>
    /// <remarks>
    /// This will determine how the value is interpreted
    /// </remarks>
    public readonly string Kind;

    /// <summary>
    /// Creates an instance of the <see cref="SimpleTypeAndFormatterAndKind"/>.
    /// </summary>
    /// <param name="dotnetTypeName">The appropriately qualified .NET type name.</param>
    /// <param name="formatterExpression">The expression that will format an instance of <paramref name="dotnetTypeName"/>.</param>
    /// <param name="kind">The expression that specifies the kind to apply when the value is formatted.</param>
    public SimpleTypeAndFormatterAndKind(string dotnetTypeName, string formatterExpression, string kind)
    {
        DotnetTypeName = dotnetTypeName;
        FormatterExpression = formatterExpression;
        Kind = kind;
    }
}