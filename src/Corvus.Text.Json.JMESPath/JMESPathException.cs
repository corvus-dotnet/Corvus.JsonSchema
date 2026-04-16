// <copyright file="JMESPathException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// Represents an error encountered while lexing, parsing, or evaluating a JMESPath expression.
/// </summary>
public class JMESPathException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JMESPathException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public JMESPathException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JMESPathException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public JMESPathException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
