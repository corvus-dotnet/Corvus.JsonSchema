using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;

namespace Corvus.Text.Json.Jsonata.Playground.Services;

/// <summary>
/// Result of a JSONata evaluation.
/// </summary>
public sealed class EvaluationResult
{
    public bool Success { get; init; }

    public string? ResultJson { get; init; }

    public string? ErrorMessage { get; init; }

    public double ElapsedMs { get; init; }
}

/// <summary>
/// Wraps <see cref="JsonataEvaluator"/> for use in the Blazor playground.
/// </summary>
public sealed class EvaluationService
{
    public async Task<EvaluationResult> EvaluateAsync(
        string expression,
        string jsonData,
        IReadOnlyDictionary<string, JsonataBinding>? bindings = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new EvaluationResult
            {
                Success = true,
                ResultJson = string.Empty,
                ElapsedMs = 0,
            };
        }

        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                string data = string.IsNullOrWhiteSpace(jsonData) ? "{}" : jsonData;

                using var doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(data));

                JsonElement result = JsonataEvaluator.Default.Evaluate(
                    expression,
                    doc.RootElement,
                    bindings);

                sw.Stop();

                string? resultText = result.ValueKind == JsonValueKind.Undefined
                    ? "/* no result */"
                    : result.GetRawText();

                return new EvaluationResult
                {
                    Success = true,
                    ResultJson = resultText,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new EvaluationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
        });
    }
}