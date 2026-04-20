using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Yaml;

namespace Corvus.Text.Json.Yaml.Playground.Services;

/// <summary>
/// Identifies which editor a conversion error originated from.
/// </summary>
public enum ErrorSource
{
    /// <summary>No specific editor (generic error).</summary>
    None,

    /// <summary>Error in the YAML input.</summary>
    Yaml,
}

/// <summary>
/// Result of a YAML to JSON conversion.
/// </summary>
public sealed class ConversionResult
{
    public bool Success { get; init; }

    public string? ResultJson { get; init; }

    public string? ErrorMessage { get; init; }

    public double ElapsedMs { get; init; }

    /// <summary>Gets which editor the error originated from.</summary>
    public ErrorSource ErrorSource { get; init; }

    /// <summary>Gets the 1-based line number of the error, if available.</summary>
    public int? ErrorLine { get; init; }

    /// <summary>Gets the 1-based column number of the error, if available.</summary>
    public int? ErrorColumn { get; init; }
}

/// <summary>
/// Wraps the YAML to JSON conversion for use in the Blazor playground.
/// </summary>
public sealed class ConversionService
{
    public async Task<ConversionResult> ConvertAsync(
        string yamlInput,
        YamlSchema schema,
        YamlDocumentMode documentMode,
        DuplicateKeyBehavior duplicateKeyBehavior)
    {
        if (string.IsNullOrWhiteSpace(yamlInput))
        {
            return new ConversionResult
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
                var options = new YamlReaderOptions
                {
                    Schema = schema,
                    DocumentMode = documentMode,
                    DuplicateKeyBehavior = duplicateKeyBehavior,
                };

                byte[] utf8Bytes = Encoding.UTF8.GetBytes(yamlInput);
                using JsonDocument doc = YamlDocument.Parse(utf8Bytes, options);

                sw.Stop();

                string resultText = FormatJson(doc.RootElement);

                return new ConversionResult
                {
                    Success = true,
                    ResultJson = resultText,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
            catch (YamlException yex)
            {
                sw.Stop();

                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = FormatYamlError(yex),
                    ErrorSource = ErrorSource.Yaml,
                    ErrorLine = yex.Line > 0 ? yex.Line : null,
                    ErrorColumn = yex.Column > 0 ? yex.Column : null,
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

    private static string FormatYamlError(YamlException ex)
    {
        if (ex.Line > 0 && ex.Column > 0)
        {
            return $"Ln {ex.Line}, Col {ex.Column}: {ex.Message}";
        }

        return ex.Message;
    }
}
