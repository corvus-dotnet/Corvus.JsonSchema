// <copyright file="AnchorKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

#if NET8_0_OR_GREATER
/// <summary>
/// Represents an anchor keyword.
/// </summary>
/// <param name="Name">The name of the keyword.</param>
/// <param name="IsDynamic">Whether the anchor is dynamic.</param>
/// <param name="IsRecursive">Whether the anchor is recursive.</param>
public record AnchorKeyword(string Name, bool IsDynamic, bool IsRecursive);
#else
/// <summary>
/// Represents an anchor keyword.
/// </summary>
public class AnchorKeyword
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnchorKeyword"/> class.
    /// </summary>
    /// <param name="name">The name of the keyword.</param>
    /// <param name="isDynamic">Whether the anchor is dynamic.</param>
    /// <param name="isRecursive">Whether the anchor is recursive.</param>
    public AnchorKeyword(string name, bool isDynamic, bool isRecursive)
    {
        this.Name = name;
        this.IsDynamic = isDynamic;
        this.IsRecursive = isRecursive;
    }

    /// <summary>
    /// Gets or sets the name of the anchor.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the anchor is a dynamic anchor.
    /// </summary>
    public bool IsDynamic { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the anchor is a recursive anchor.
    /// </summary>
    public bool IsRecursive { get; set; }
}
#endif