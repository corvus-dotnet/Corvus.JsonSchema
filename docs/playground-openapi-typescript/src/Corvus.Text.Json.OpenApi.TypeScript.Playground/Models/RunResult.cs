using System.Text.Json.Serialization;

namespace Corvus.Text.Json.OpenApi.TypeScript.Playground.Models;

/// <summary>
/// The result of transpiling + running the user's TypeScript in the browser (returned from the
/// playgroundInterop.transpileAndRun JS interop).
/// </summary>
public class RunResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the run completed without a build or runtime error.
    /// </summary>
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    /// <summary>
    /// Gets or sets the captured console output lines, in order.
    /// </summary>
    [JsonPropertyName("output")]
    public List<RunConsoleLine> Output { get; set; } = [];

    /// <summary>
    /// Gets or sets a build or runtime error message, if the run failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// A single captured console line.
/// </summary>
public class RunConsoleLine
{
    /// <summary>
    /// Gets or sets the console method used: "log", "error", "warn", or "info".
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "log";

    /// <summary>
    /// Gets or sets the formatted line text.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}