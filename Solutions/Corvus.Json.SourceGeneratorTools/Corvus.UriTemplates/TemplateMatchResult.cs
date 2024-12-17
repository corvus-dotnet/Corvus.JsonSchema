// <copyright file="TemplateMatchResult.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.UriTemplates;

#if NET8_0_OR_GREATER
/// <summary>
/// A result from matching a template in a template table.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
/// <param name="Result">The user-specified result for the match.</param>
/// <param name="Parser">The URI template parser that matched.</param>
public readonly record struct TemplateMatchResult<T>(T Result, IUriTemplateParser Parser);
#else
/// <summary>
/// A result from matching a template in a template table.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
public readonly struct TemplateMatchResult<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateMatchResult{T}"/> struct.
    /// </summary>
    /// <param name="result">The user-specified result for the match.</param>
    /// <param name="parser">The URI template parser that matched.</param>
    public TemplateMatchResult(T result, IUriTemplateParser parser)
    {
        this.Result = result;
        this.Parser = parser;
    }

    /// <summary>
    /// Gets the match result.
    /// </summary>
    public T Result { get; }

    /// <summary>
    /// Gets the <see cref="IUriTemplateParser"/>.
    /// </summary>
    public IUriTemplateParser Parser { get; }
}
#endif