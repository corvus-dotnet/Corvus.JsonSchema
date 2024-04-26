// <copyright file="RefKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

#if NET8_0_OR_GREATER
/// <summary>
/// Represents a ref-like keyword.
/// </summary>
/// <param name="Name">The name of the keyword.</param>
/// <param name="RefKind">The strategy to use for resolving the referenced value.</param>
public record RefKeyword(string Name, RefKind RefKind);
#else
/// <summary>
/// Represents a ref-like keyword.
/// </summary>
public class RefKeyword
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefKeyword"/> class.
    /// </summary>
    /// <param name="name">The name of the keyword.</param>
    /// <param name="refKind">The strategy to use for resolving the referenced value.</param>
    public RefKeyword(string name, RefKind refKind)
    {
        this.Name = name;
        this.RefKind = refKind;
    }

    /// <summary>
    /// Gets or sets the name of the keyword.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the strategy to use for resolving the referenced value.
    /// </summary>
    public RefKind RefKind { get; set; }
}
#endif