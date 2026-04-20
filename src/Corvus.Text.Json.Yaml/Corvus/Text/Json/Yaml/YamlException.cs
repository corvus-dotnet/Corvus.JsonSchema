// <copyright file="YamlException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml;
#else
namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// Exception thrown when YAML parsing or conversion fails.
/// </summary>
public class YamlException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="line">The 1-based line number where the error occurred.</param>
    /// <param name="column">The 1-based column number where the error occurred.</param>
    public YamlException(string message, int line, int column)
        : base(FormatMessage(message, line, column))
    {
        this.Line = line;
        this.Column = column;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="line">The 1-based line number where the error occurred.</param>
    /// <param name="column">The 1-based column number where the error occurred.</param>
    /// <param name="innerException">The inner exception.</param>
    public YamlException(string message, int line, int column, Exception innerException)
        : base(FormatMessage(message, line, column), innerException)
    {
        this.Line = line;
        this.Column = column;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public YamlException(string message)
        : base(message)
    {
        this.Line = 0;
        this.Column = 0;
    }

    /// <summary>
    /// Gets the 1-based line number where the error occurred, or 0 if unknown.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the 1-based column number where the error occurred, or 0 if unknown.
    /// </summary>
    public int Column { get; }

    private static string FormatMessage(string message, int line, int column)
    {
        return $"({line},{column}): {message}";
    }
}