namespace Corvus.Text.Json.TypeScript.Playground.Models;

/// <summary>
/// A built-in playground example: a JSON Schema plus a matching TypeScript snippet that uses the generated
/// module.
/// </summary>
/// <param name="Id">A stable identifier used in the sample picker and URL state.</param>
/// <param name="DisplayName">The label shown in the sample picker.</param>
/// <param name="Schema">The JSON Schema source.</param>
/// <param name="UserCode">The TypeScript snippet that imports from "./generated.js" and runs.</param>
public record PlaygroundSample(
    string Id,
    string DisplayName,
    string Schema,
    string UserCode);
