using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Toon;

namespace Corvus.Text.Json.Toon.Playground.Services;

/// <summary>
/// The conversion direction.
/// </summary>
public enum ConversionDirection
{
    /// <summary>TOON to JSON.</summary>
    ToonToJson,

    /// <summary>JSON to TOON.</summary>
    JsonToToon,
}

/// <summary>
/// Identifies which editor a conversion error originated from.
/// </summary>
public enum ErrorSource
{
    /// <summary>No specific editor.</summary>
    None,

    /// <summary>Error in the TOON input.</summary>
    Toon,

    /// <summary>Error in the JSON input.</summary>
    Json,
}

/// <summary>
/// Result of a conversion.
/// </summary>
public sealed class ConversionResult
{
    public bool Success { get; init; }

    public string? ResultText { get; init; }

    public string? ErrorMessage { get; init; }

    public double ElapsedMs { get; init; }

    public ErrorSource ErrorSource { get; init; }

    public int? ErrorLine { get; init; }

    public int? ErrorColumn { get; init; }
}

/// <summary>
/// Wraps TOON ↔ JSON conversions for the Blazor playground.
/// </summary>
public sealed class ConversionService
{
    public async Task<ConversionResult> ConvertToonToJsonAsync(
        string toonInput,
        bool strict,
        int indentSize,
        ToonPathExpansion pathExpansion)
    {
        if (string.IsNullOrWhiteSpace(toonInput))
        {
            return new ConversionResult
            {
                Success = true,
                ResultText = string.Empty,
                ElapsedMs = 0,
            };
        }

        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var options = new ToonReaderOptions
                {
                    Strict = strict,
                    IndentSize = indentSize,
                    ExpandPaths = pathExpansion,
                };

                string json = ToonDocument.ConvertToJsonString(toonInput, options);
                string resultText = FormatJson(json);
                sw.Stop();

                return new ConversionResult
                {
                    Success = true,
                    ResultText = resultText,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
            catch (ToonException tex)
            {
                sw.Stop();
                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = FormatToonError(tex),
                    ErrorSource = ErrorSource.Toon,
                    ErrorLine = tex.Line > 0 ? tex.Line : null,
                    ErrorColumn = tex.Column > 0 ? tex.Column : null,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = FixBrokenSRFormat(ex.Message),
                    ErrorSource = ErrorSource.Toon,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
        });
    }

    public async Task<ConversionResult> ConvertJsonToToonAsync(
        string jsonInput,
        int indentSize,
        ToonDelimiter delimiter,
        ToonKeyFolding keyFolding,
        int flattenDepth)
    {
        if (string.IsNullOrWhiteSpace(jsonInput))
        {
            return new ConversionResult
            {
                Success = true,
                ResultText = string.Empty,
                ElapsedMs = 0,
            };
        }

        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var options = new ToonWriterOptions
                {
                    IndentSize = indentSize,
                    Delimiter = delimiter,
                    KeyFolding = keyFolding,
                    FlattenDepth = flattenDepth,
                };

                string resultToon = ToonDocument.ConvertToToonString(jsonInput, options);
                sw.Stop();

                return new ConversionResult
                {
                    Success = true,
                    ResultText = resultToon,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
            catch (JsonException jex)
            {
                sw.Stop();
                int? errorLine = jex.LineNumber.HasValue ? (int)jex.LineNumber.Value + 1 : null;
                int? errorColumn = jex.BytePositionInLine.HasValue ? (int)jex.BytePositionInLine.Value + 1 : null;

                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = jex.Message,
                    ErrorSource = ErrorSource.Json,
                    ErrorLine = errorLine,
                    ErrorColumn = errorColumn,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = FixBrokenSRFormat(ex.Message),
                    ErrorSource = ErrorSource.Json,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
        });
    }

    private static string FormatJson(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            doc.RootElement.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string FormatToonError(ToonException ex)
    {
        string message = FixBrokenSRFormat(ex.Message);

        if (ex.Line > 0 && ex.Column > 0)
        {
            return $"Ln {ex.Line}, Col {ex.Column}: {message}";
        }

        return message;
    }

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
        catch (FormatException)
        {
            return message;
        }
    }
}