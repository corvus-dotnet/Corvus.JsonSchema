// <copyright file="JsonSchemaFormatMode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.RuntimeEvaluator;

/// <summary>
/// How a particular <c>format</c> is treated, overriding <see cref="JsonSchemaEvaluatorOptions.AssertFormat"/>
/// and the dialect default for that format.
/// </summary>
public enum JsonSchemaFormatMode
{
    /// <summary>The format is asserted: a value that does not conform fails validation.</summary>
    Assert,

    /// <summary>The format is not asserted; it is reported as an annotation only.</summary>
    Disable,

    /// <summary>
    /// The format is checked but never fails validation: a value that does not conform is reported as a
    /// matching result carrying a warning message (string formats only; numeric formats assert).
    /// </summary>
    Warning,
}