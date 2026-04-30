using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Playground.Models;

/// <summary>
/// Result of a code generation operation.
/// </summary>
public class GenerationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether code generation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the generated code files (hidden from user, used for compilation).
    /// </summary>
    public IReadOnlyCollection<GeneratedCodeFile> GeneratedFiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the type map entries for display.
    /// </summary>
    public IReadOnlyList<TypeMapEntry> TypeMap { get; set; } = [];

    /// <summary>
    /// Gets or sets the error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets schema-level errors with file and position info.
    /// </summary>
    public IReadOnlyList<SchemaError> SchemaErrors { get; set; } = [];
}

/// <summary>
/// An error associated with a specific schema file, optionally with position info.
/// </summary>
/// <param name="SchemaName">The schema filename (e.g. "person.json").</param>
/// <param name="Message">The error description.</param>
/// <param name="LineNumber">0-based line number, or null if unknown.</param>
/// <param name="Column">0-based column, or null if unknown.</param>
public record SchemaError(
    string SchemaName,
    string Message,
    long? LineNumber,
    long? Column);
