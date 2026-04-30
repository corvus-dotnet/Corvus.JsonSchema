// <copyright file="JsonPathException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Represents an error encountered while lexing, parsing, or evaluating a JSONPath expression.
/// </summary>
public class JsonPathException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPathException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JsonPathException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPathException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="position">The 0-based byte offset in the expression where the error occurred, or -1 if unknown.</param>
    public JsonPathException(string message, int position)
        : base(message)
    {
        Position = position;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPathException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public JsonPathException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets the 0-based byte offset in the expression where the error occurred,
    /// or -1 if the position is unknown.
    /// </summary>
    public int Position { get; } = -1;
}
