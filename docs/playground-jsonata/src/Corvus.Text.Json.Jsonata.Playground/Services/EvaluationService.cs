using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;

namespace Corvus.Text.Json.Jsonata.Playground.Services;

/// <summary>
/// Identifies which editor an evaluation error originated from.
/// </summary>
public enum ErrorSource
{
    /// <summary>No specific editor (generic error).</summary>
    None,

    /// <summary>Error in the JSONata expression (parse or runtime).</summary>
    Expression,

    /// <summary>Error parsing the JSON data input.</summary>
    Data,
}

/// <summary>
/// Result of a JSONata evaluation.
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
    /// Gets the zero-based character offset in the expression where the error occurred.
    /// Only set when <see cref="ErrorSource"/> is <see cref="Services.ErrorSource.Expression"/>.
    /// </summary>
    public int? ErrorPosition { get; init; }

    /// <summary>
    /// Gets the token or fragment relevant to the error.
    /// Only set when <see cref="ErrorSource"/> is <see cref="Services.ErrorSource.Expression"/>.
    /// </summary>
    public string? ErrorToken { get; init; }

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
                    : FormatWithG15Numbers(result);

                return new EvaluationResult
                {
                    Success = true,
                    ResultJson = resultText,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
            catch (JsonataException jex)
            {
                sw.Stop();
                return new EvaluationResult
                {
                    Success = false,
                    ErrorMessage = jex.Message,
                    ErrorSource = ErrorSource.Expression,
                    ErrorPosition = jex.Position,
                    ErrorToken = jex.Token,
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
    /// <para>
    /// When <c>SR.UsingResourceKeys()</c> returns <see langword="true"/> (as it does in WASM),
    /// <c>SR.Format(format, args)</c> falls back to <c>string.Join(", ", format, args)</c>
    /// instead of calling <see cref="string.Format(string,object[])"/>. This affects
    /// <c>System.Text.Json</c> and other runtime libraries.
    /// </para>
    /// <para>
    /// The resulting message looks like:
    /// <c>'{0}' is an invalid end of a number. Expected a delimiter., r LineNumber: 1 | ...</c>
    /// </para>
    /// <para>
    /// This method detects the pattern (unsubstituted <c>{0}</c> placeholders with
    /// comma-separated arguments appended), extracts the arguments, and applies
    /// <see cref="string.Format(string,object[])"/> properly.
    /// </para>
    /// </summary>
    private static string FixBrokenSRFormat(string message)
    {
        if (!message.Contains("{0}"))
        {
            return message;
        }

        // Strip the position suffix that JsonException appends (e.g., " LineNumber: 1 | BytePositionInLine: 39.")
        string suffix = string.Empty;
        Match posMatch = Regex.Match(message, @"\s*LineNumber: \d+ \| BytePositionInLine: \d+\.\s*$");
        string body = message;
        if (posMatch.Success)
        {
            suffix = posMatch.Value;
            body = message[..posMatch.Index];
        }

        // Find the highest numbered placeholder to determine argument count
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

        // SR.Format joins as: "format_string, arg0, arg1, ..."
        // Split from the right to peel off argCount arguments
        string remaining = body;
        string[] args = new string[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            int lastComma = remaining.LastIndexOf(", ", StringComparison.Ordinal);
            if (lastComma < 0)
            {
                return message; // Can't parse — return original
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

    /// <summary>
    /// Serializes a <see cref="JsonElement"/> to a JSON string, formatting all
    /// numbers with G15 precision to match JavaScript's <c>Number(val.toPrecision(15))</c>
    /// output semantics (e.g. <c>0.5</c> instead of <c>0.5000000000000001</c>).
    /// </summary>
    private static string FormatWithG15Numbers(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteG15(element, writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteG15(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    WriteG15(prop.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteG15(item, writer);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.Number:
                double d = element.GetDouble();
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    writer.WriteNullValue();
                }
                else
                {
                    // G15 matches JavaScript's toPrecision(15) semantics.
                    writer.WriteRawValue(d.ToString("G15", CultureInfo.InvariantCulture));
                }

                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}