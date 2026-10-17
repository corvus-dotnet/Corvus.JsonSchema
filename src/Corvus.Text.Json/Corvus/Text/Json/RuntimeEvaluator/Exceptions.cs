// <copyright file="Exceptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.RuntimeEvaluator;

/// <summary>
/// Thrown when a schema cannot be compiled (for example, an unresolvable <c>$ref</c>).
/// </summary>
/// <remarks>
/// A schema is also refused for a <c>pattern</c> or <c>patternProperties</c> pattern that is not a valid ECMA-262
/// regular expression, and for one that is over a limit of size. The two have different messages. The first names
/// the pattern and says what is wrong and where. The second says that the pattern is too large and names the limit
/// it is over, and does not say that the pattern is not valid.
/// </remarks>
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