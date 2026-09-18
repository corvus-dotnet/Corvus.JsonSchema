// <copyright file="IOrderingLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// An <see cref="ILanguageProvider"/> that chooses how the names that reach generated code are ordered: property
/// declarations, the subschemas of a keyword, and documentation keywords.
/// </summary>
/// <remarks>
/// <para>
/// Before it generates code with a provider that implements this interface, <see cref="JsonSchemaTypeBuilder"/> sets
/// <see cref="TypeDeclaration.OrderingComparer"/> to <see cref="OrderingComparer"/> on every type declaration it has
/// built. With any other provider the type declarations use <see cref="Comparer{T}.Default"/>, which compares with the
/// current culture.
/// </para>
/// <para>
/// It is a separate interface rather than a default interface member because the library targets netstandard2.0.
/// </para>
/// </remarks>
public interface IOrderingLanguageProvider : ILanguageProvider
{
    /// <summary>
    /// Gets the comparer with which names that reach generated code are ordered.
    /// </summary>
    IComparer<string> OrderingComparer { get; }
}