using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Text.Json;
using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.Playground.Services;

/// <summary>
/// Identifies which editor an evaluation error originated from.
/// </summary>
public enum ErrorSource
{
    None,
    Rule,
    Data,
}

/// <summary>
/// Result of a JsonLogic evaluation.
/// </summary>
public sealed class EvaluationResult
{
    public bool Success { get; init; }

    public string? ResultJson { get; init; }

    public string? ErrorMessage { get; init; }

    public double ElapsedMs { get; init; }

    public ErrorSource ErrorSource { get; init; }
}

/// <summary>
/// Wraps <see cref="JsonLogicEvaluator"/> for use in the Blazor playground.
/// </summary>
public sealed class EvaluationService
{
    // Fresh evaluator per call — avoids cache holding references to disposed documents
    private readonly SemaphoreSlim evalLock = new(1, 1);

    public async Task<EvaluationResult> EvaluateAsync(string ruleJson, string dataJson)
    {
        if (string.IsNullOrWhiteSpace(ruleJson))
        {
            return new EvaluationResult
            {
                Success = true,
                ResultJson = string.Empty,
                ElapsedMs = 0,
            };
        }

        await evalLock.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();

                // Parse rule
                ParsedJsonDocument<JsonElement> ruleDoc;
                try
                {
                    ruleDoc = ParsedJsonDocument<JsonElement>.Parse(
                        Encoding.UTF8.GetBytes(ruleJson));
                }
                catch (JsonException ex)
                {
                    sw.Stop();
                    return new EvaluationResult
                    {
                        Success = false,
                        ErrorMessage = "Rule JSON: " + FixBrokenSRFormat(ex.Message),
                        ErrorSource = ErrorSource.Rule,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    };
                }

                // Parse data
                ParsedJsonDocument<JsonElement> dataDoc;
                try
                {
                    string data = string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson;
                    dataDoc = ParsedJsonDocument<JsonElement>.Parse(
                        Encoding.UTF8.GetBytes(data));
                }
                catch (JsonException ex)
                {
                    ruleDoc.Dispose();
                    sw.Stop();
                    return new EvaluationResult
                    {
                        Success = false,
                        ErrorMessage = "Data JSON: " + FixBrokenSRFormat(ex.Message),
                        ErrorSource = ErrorSource.Data,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    };
                }

                try
                {
                    // Use a fresh evaluator each time so no cached references
                    // to previously-disposed documents survive across calls.
                    var eval = new JsonLogicEvaluator(
                        new Dictionary<string, IOperatorCompiler>());

                    var rule = new JsonLogicRule(ruleDoc.RootElement);
                    JsonElement result = eval.Evaluate(in rule, dataDoc.RootElement);

                    // Format result BEFORE disposing documents
                    string resultText;
                    try
                    {
                        resultText = result.ValueKind == JsonValueKind.Undefined
                            ? "/* no result */"
                            : FormatJson(result);
                    }
                    catch (ObjectDisposedException)
                    {
                        resultText = "/* result unavailable — backing document was disposed */";
                    }

                    sw.Stop();

                    return new EvaluationResult
                    {
                        Success = true,
                        ResultJson = resultText,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    };
                }
                catch (Exception ex) when (ex is ObjectDisposedException)
                {
                    sw.Stop();
                    return new EvaluationResult
                    {
                        Success = false,
                        ErrorMessage = "Cannot access a disposed object (JsonDocument). Try again.",
                        ErrorSource = ErrorSource.Rule,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    };
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    return new EvaluationResult
                    {
                        Success = false,
                        ErrorMessage = FixBrokenSRFormat(ex.Message),
                        ErrorSource = ErrorSource.Rule,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    };
                }
                finally
                {
                    ruleDoc.Dispose();
                    dataDoc.Dispose();
                }
            });
        }
        finally
        {
            evalLock.Release();
        }
    }

    private static string FixBrokenSRFormat(string message)
    {
        // Detect unresolved SR key patterns (Blazor WASM trims resource strings)
        if (message.Contains("ObjectDisposed_"))
        {
            return "Cannot access a disposed object.";
        }

        if (!message.Contains("{0}"))
        {
            return message;
        }

        string suffix = string.Empty;
        Match posMatch = Regex.Match(message, @"\s*LineNumber: \d+ \| BytePositionInLine: \d+\.\s*$");
        string body = message;
        if (posMatch.Success)
        {
            suffix = posMatch.Value;
            body = message[..posMatch.Index];
        }

        int maxPlaceholder = -1;
        for (int i = 9; i >= 0; i--)
        {
            if (body.Contains($"{{{i}}}"))
            {
                maxPlaceholder = i;
                break;
            }
        }

        if (maxPlaceholder < 0)
        {
            return message;
        }

        int argCount = maxPlaceholder + 1;
        string remaining = body;
        string[] args = new string[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            int lastComma = remaining.LastIndexOf(", ", StringComparison.Ordinal);
            if (lastComma < 0)
            {
                return message;
            }

            args[i] = remaining[(lastComma + 2)..];
            remaining = remaining[..lastComma];
        }

        try
        {
            return string.Format(remaining, args) + suffix;
        }
        catch
        {
            return message;
        }
    }

    private static string FormatJson(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            element.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
