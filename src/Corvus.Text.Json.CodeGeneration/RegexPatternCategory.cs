// <copyright file="RegexPatternCategory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Classifies a regular expression pattern for potential inline code generation
/// optimizations, avoiding a full <see cref="System.Text.RegularExpressions.Regex"/> object.
/// </summary>
internal enum RegexPatternCategory
{
    /// <summary>
    /// The pattern requires a full regular expression.
    /// </summary>
    FullRegex,

    /// <summary>
    /// The pattern always matches any string (e.g. <c>.*</c>, <c>^.*$</c>, <c>[\s\S]*</c>).
    /// No validation is required.
    /// </summary>
    Noop,

    /// <summary>
    /// The pattern matches any non-empty string (e.g. <c>.+</c>, <c>^.+$</c>, <c>.</c>).
    /// </summary>
    NonEmpty,

    /// <summary>
    /// The pattern matches strings starting with a literal prefix (e.g. <c>^/api</c>, <c>^x-</c>).
    /// The <see cref="CodeGenerationExtensions.ClassifyRegexPattern"/> method extracts the prefix.
    /// </summary>
    Prefix,

    /// <summary>
    /// The pattern matches strings whose length is within a range (e.g. <c>^.{1,256}$</c>).
    /// </summary>
    Range,
}