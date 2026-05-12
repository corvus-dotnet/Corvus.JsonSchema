using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.Playground.Models;

/// <summary>
/// Describes a custom JsonLogic operator with its compiler and Blockly block metadata.
/// Returned by the user's factory method.
/// </summary>
public sealed record CustomOperatorDefinition(
    IOperatorCompiler Compiler,
    int MinArgs = 0,
    int? MaxArgs = null,
    string? Description = null);
