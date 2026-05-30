#if STJ && TOON
namespace Corvus.Toon;
#else
namespace Corvus.Text.Json.Toon;
#endif

/// <summary>
/// Exception thrown when TOON parsing or conversion fails.
/// </summary>
public class ToonException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToonException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="line">The 1-based line number where the error occurred.</param>
    /// <param name="column">The 1-based column number where the error occurred.</param>
    public ToonException(string message, int line, int column)
        : base(FormatMessage(message, line, column))
    {
        this.Line = line;
        this.Column = column;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToonException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ToonException(string message)
        : base(message)
    {
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
        return SR.Format(SR.ToonExceptionLineColumn, line, column, message);
    }
}