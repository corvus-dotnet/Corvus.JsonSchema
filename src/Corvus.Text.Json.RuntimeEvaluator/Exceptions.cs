// <copyright file="Exceptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.RuntimeEvaluator;

/// <summary>
/// Thrown when a schema cannot be compiled (for example, an unresolvable <c>$ref</c>).
/// </summary>
public sealed class JsonSchemaCompilationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaCompilationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public JsonSchemaCompilationException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Thrown when evaluation cannot proceed (for example, runaway recursion through in-place applicators).
/// </summary>
public sealed class JsonSchemaEvaluationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaEvaluationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public JsonSchemaEvaluationException(string message)
        : base(message)
    {
    }
}
