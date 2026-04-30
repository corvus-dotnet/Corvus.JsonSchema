using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;

namespace Corvus.Text.Json.JsonPath.Playground.Services;

/// <summary>
/// Identifies which editor an evaluation error originated from.
/// </summary>
public enum ErrorSource
{
    /// <summary>No specific editor (generic error).</summary>
    None,

    /// <summary>Error in the JSONPath expression.</summary>
    Expression,

    /// <summary>Error parsing the JSON data input.</summary>
    Data,
}

/// <summary>
/// Result of a JSONPath evaluation.
/// </summary>
public sealed class EvaluationResult
{
    public bool Success { get; init; }

    public string? ResultJson { get; init; }

    public string? ErrorMessage { get; init; }

    public double ElapsedMs { get; init; }

    /// <summary>Gets which editor the error originated from.</summary>
    public ErrorSource ErrorSource { get; init; }

    /// <summary>
    /// Gets the 1-based line number where a JSON data parse error occurred.
    /// Only set when <see cref="ErrorSource"/> is <see cref="Services.ErrorSource.Data"/>.
    /// </summary>
    public int? ErrorLine { get; init; }

    /// <summary>
    /// Gets the 1-based column number where a JSON data parse error occurred.
    /// Only set when <see cref="ErrorSource"/> is <see cref="Services.ErrorSource.Data"/>.
    /// </summary>
    public int? ErrorColumn { get; init; }
}

/// <summary>
/// Wraps <see cref="JsonPathEvaluator"/> for use in the Blazor playground.
/// </summary>
public sealed class EvaluationService
{
    private JsonPathEvaluator cachedEvaluator = JsonPathEvaluator.Default;
    private Dictionary<string, IJsonPathFunction>? cachedFunctions;

    public async Task<EvaluationResult> EvaluateAsync(
        string expression,
        string jsonData,
        Dictionary<string, IJsonPathFunction>? customFunctions = null)
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

                var evaluator = this.GetOrCreateEvaluator(customFunctions);

                JsonElement result = evaluator.Query(
                    expression,
                    doc.RootElement);

                sw.Stop();

                string? resultText = result.ValueKind == JsonValueKind.Undefined
                    ? "/* no result */"
                    : FormatJson(result);

                return new EvaluationResult
                {
                    Success = true,
                    ResultJson = resultText,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
            catch (JsonPathException jpex)
            {
                sw.Stop();

                // Convert byte offset to 1-based line and column
                int? errorLine = null;
                int? errorColumn = null;
                if (jpex.Position >= 0)
                {
                    PositionToLineColumn(expression, jpex.Position, out int line, out int col);
                    errorLine = line;
                    errorColumn = col;
                }

                return new EvaluationResult
                {
                    Success = false,
                    ErrorMessage = FormatExpressionError(jpex.Message, errorLine, errorColumn),
                    ErrorSource = ErrorSource.Expression,
                    ErrorLine = errorLine,
                    ErrorColumn = errorColumn,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
            catch (JsonException jsonEx)
            {
                sw.Stop();
                return new EvaluationResult
                {
                    Success = false,
                    ErrorMessage = FixBrokenSRFormat(jsonEx.Message),
                    ErrorSource = ErrorSource.Data,
                    ErrorLine = jsonEx.LineNumber.HasValue ? (int)jsonEx.LineNumber.Value + 1 : null,
                    ErrorColumn = jsonEx.BytePositionInLine.HasValue ? (int)jsonEx.BytePositionInLine.Value + 1 : null,
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
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
        });
    }

    /// <summary>
    /// Fixes broken <c>SR.Format</c> output in Blazor WASM.
    /// </summary>
    private static string FixBrokenSRFormat(string message)
    {
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

    /// <summary>
    /// Converts a 0-based byte offset in a string to 1-based line and column numbers.
    /// </summary>
    private static void PositionToLineColumn(string text, int byteOffset, out int line, out int column)
    {
        // The expression is UTF-8 encoded by the JSONPath lexer, but the playground
        // receives it as a .NET string. Convert to UTF-8 to find the correct line/column.
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        int clampedOffset = Math.Min(byteOffset, utf8.Length);

        line = 1;
        int lineStart = 0;
        for (int i = 0; i < clampedOffset; i++)
        {
            if (utf8[i] == (byte)'\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        column = clampedOffset - lineStart + 1;
    }

    /// <summary>
    /// Formats an expression error message with line and column information.
    /// </summary>
    private static string FormatExpressionError(string message, int? line, int? column)
    {
        if (line is int l && column is int c)
        {
            return $"Ln {l}, Col {c}: {message}";
        }

        return message;
    }

    /// <summary>
    /// Returns a cached evaluator if the functions set hasn't changed, or creates a new one.
    /// </summary>
    private JsonPathEvaluator GetOrCreateEvaluator(Dictionary<string, IJsonPathFunction>? customFunctions)
    {
        if (ReferenceEquals(customFunctions, this.cachedFunctions))
        {
            return this.cachedEvaluator;
        }

        this.cachedFunctions = customFunctions;

        if (customFunctions is null || customFunctions.Count == 0)
        {
            this.cachedEvaluator = JsonPathEvaluator.Default;
        }
        else
        {
            this.cachedEvaluator = new JsonPathEvaluator(customFunctions);
        }

        return this.cachedEvaluator;
    }
}
