using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
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
                    : FormatWithG15Numbers(result);

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