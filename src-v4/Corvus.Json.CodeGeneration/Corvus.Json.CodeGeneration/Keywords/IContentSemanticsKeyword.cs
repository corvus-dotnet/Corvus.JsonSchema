// <copyright file="IContentSemanticsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword which defines content semantics for a type declaration.
/// </summary>
public interface IContentSemanticsKeyword : IKeyword
{
    /// <summary>
    /// Gets the <see cref="ContentSemantics"/> for the keyword.
    /// </summary>
    ContentEncodingSemantics ContentSemantics { get; }
}