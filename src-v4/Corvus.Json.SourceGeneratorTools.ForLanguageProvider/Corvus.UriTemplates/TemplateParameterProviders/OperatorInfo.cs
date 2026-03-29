// <copyright file="OperatorInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.UriTemplates.TemplateParameterProviders;

/// <summary>
/// Gets the operator info for a variable specification.
/// </summary>
public readonly struct OperatorInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperatorInfo"/> struct.
    /// </summary>
    /// <param name="default">The defualt value.</param>
    /// <param name="first">If this is the first parameter.</param>
    /// <param name="separator">The separator.</param>
    /// <param name="named">If this is a named parameter.</param>
    /// <param name="ifEmpty">The value to use if empty.</param>
    /// <param name="allowReserved">Whether to allow reserved characters.</param>
    public OperatorInfo(bool @default, char first, char separator, bool named, string ifEmpty, bool allowReserved)
    {
        this.Default = @default;
        this.First = first;
        this.Separator = separator;
        this.Named = named;
        this.IfEmpty = ifEmpty;
        this.AllowReserved = allowReserved;
    }

    /// <summary>
    /// Gets a value indicating whether this is default.
    /// </summary>
    public bool Default { get; }

    /// <summary>
    /// Gets the first element.
    /// </summary>
    public char First { get; }

    /// <summary>
    /// Gets the separator.
    /// </summary>
    public char Separator { get; }

    /// <summary>
    /// Gets a value indicating whether this is named.
    /// </summary>
    public bool Named { get; }

    /// <summary>
    /// Gets the string to use if empty.
    /// </summary>
    public string IfEmpty { get; }

    /// <summary>
    /// Gets a value indicating whether this allows reserved symbols.
    /// </summary>
    public bool AllowReserved { get; }
}