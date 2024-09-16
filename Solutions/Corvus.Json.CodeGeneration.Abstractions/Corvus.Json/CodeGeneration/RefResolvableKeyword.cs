// <copyright file="RefResolvableKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

#if NET8_0_OR_GREATER
/// <summary>
/// Represents a ref resolvable keyword.
/// </summary>
/// <param name="Name">The name of the keyword.</param>
/// <param name="RefResolvablePropertyKind">The strategy to use for inspecting the property for resolvable schema.</param>
public record RefResolvableKeyword(string Name, RefResolvablePropertyKind RefResolvablePropertyKind);
#else
/// <summary>
/// Represents a ref resolvable keyword.
/// </summary>
public class RefResolvableKeyword
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefResolvableKeyword"/> class.
    /// </summary>
    /// <param name="name">The name of the keyword.</param>
    /// <param name="refResolvablePropertyKind">The strategy to use for inspecting the property for resolvable schema.</param>
    public RefResolvableKeyword(string name, RefResolvablePropertyKind refResolvablePropertyKind)
    {
        this.Name = name;
        this.RefResolvablePropertyKind = refResolvablePropertyKind;
    }

    /// <summary>
    /// Gets or sets the name of the keyword.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the strategy to use for inspecting the property for resolvable schema.
    /// </summary>
    public RefResolvablePropertyKind RefResolvablePropertyKind { get; set; }
}
#endif