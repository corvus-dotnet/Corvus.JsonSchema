using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Yaml;

namespace Corvus.Text.Json.Yaml.Playground.Services;

/// <summary>
/// The conversion direction.
/// </summary>
public enum ConversionDirection
{
    /// <summary>YAML to JSON.</summary>
    YamlToJson,

    /// <summary>JSON to YAML.</summary>
    JsonToYaml,
}

/// <summary>
/// Identifies which editor a conversion error originated from.
/// </summary>
public enum ErrorSource
{
    /// <summary>No specific editor (generic error).</summary>
    None,

    /// <summary>Error in the YAML input.</summary>
    Yaml,

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

    /// <summary>Gets which editor the error originated from.</summary>
    public ErrorSource ErrorSource { get; init; }

    /// <summary>Gets the 1-based line number of the error, if available.</summary>
    public int? ErrorLine { get; init; }

    /// <summary>Gets the 1-based column number of the error, if available.</summary>
    public int? ErrorColumn { get; init; }
}

/// <summary>
/// A single materialized YAML parse event (non-ref-struct copy of <see cref="YamlEvent"/>).
/// </summary>
public sealed class EventRecord
{
    public YamlEventType Type { get; init; }

    public string Value { get; init; } = "";

    public string Anchor { get; init; } = "";

    public string Tag { get; init; } = "";

    public YamlScalarStyle ScalarStyle { get; init; }

    public int Line { get; init; }

    public int Column { get; init; }

    public bool IsImplicit { get; init; }

    public bool IsFlowStyle { get; init; }
}

/// <summary>
/// Wraps YAML ↔ JSON conversions and event enumeration for the Blazor playground.
/// </summary>
public sealed class ConversionService
{
    public async Task<ConversionResult> ConvertYamlToJsonAsync(
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
                ResultText = string.Empty,
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
                    ResultText = resultText,
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

    public async Task<ConversionResult> ConvertJsonToYamlAsync(string jsonInput)
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
                string resultYaml = YamlDocument.ConvertToYamlString(jsonInput);
                sw.Stop();

                return new ConversionResult
                {
                    Success = true,
                    ResultText = resultYaml,
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
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                };
            }
        });
    }

    public async Task<List<EventRecord>> GetEventsAsync(
        string yaml,
        YamlReaderOptions options)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return [];
        }

        return await Task.Run(() =>
        {
            var events = new List<EventRecord>();

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(yaml);
            YamlDocument.EnumerateEvents(
                utf8Bytes.AsSpan(),
                (in YamlEvent e) =>
                {
                    events.Add(new EventRecord
                    {
                        Type = e.Type,
                        Value = e.Value.IsEmpty ? "" : Encoding.UTF8.GetString(e.Value),
                        Anchor = e.Anchor.IsEmpty ? "" : Encoding.UTF8.GetString(e.Anchor),
                        Tag = e.Tag.IsEmpty ? "" : Encoding.UTF8.GetString(e.Tag),
                        ScalarStyle = e.ScalarStyle,
                        Line = e.Line,
                        Column = e.Column,
                        IsImplicit = e.IsImplicit,
                        IsFlowStyle = e.IsFlowStyle,
                    });
                    return true;
                },
                options);

            return events;
        });
    }

    /// <summary>
    /// Formats a list of event records in canonical YAML test suite notation.
    /// </summary>
    public static string FormatEventStream(List<EventRecord> events)
    {
        var sb = new StringBuilder();

        foreach (var e in events)
        {
            string prefix = "";
            if (e.Anchor.Length > 0)
            {
                prefix += $"&{e.Anchor} ";
            }

            if (e.Tag.Length > 0)
            {
                prefix += $"<{e.Tag}> ";
            }

            switch (e.Type)
            {
                case YamlEventType.StreamStart:
                    sb.AppendLine("+STR");
                    break;
                case YamlEventType.StreamEnd:
                    sb.AppendLine("-STR");
                    break;
                case YamlEventType.DocumentStart:
                    sb.AppendLine(e.IsImplicit ? "+DOC" : "+DOC ---");
                    break;
                case YamlEventType.DocumentEnd:
                    sb.AppendLine(e.IsImplicit ? "-DOC" : "-DOC ...");
                    break;
                case YamlEventType.MappingStart:
                    sb.Append(prefix);
                    sb.AppendLine(e.IsFlowStyle ? "+MAP {}" : "+MAP");
                    break;
                case YamlEventType.MappingEnd:
                    sb.AppendLine("-MAP");
                    break;
                case YamlEventType.SequenceStart:
                    sb.Append(prefix);
                    sb.AppendLine(e.IsFlowStyle ? "+SEQ []" : "+SEQ");
                    break;
                case YamlEventType.SequenceEnd:
                    sb.AppendLine("-SEQ");
                    break;
                case YamlEventType.Scalar:
                    sb.Append(prefix);
                    char styleChar = e.ScalarStyle switch
                    {
                        YamlScalarStyle.Plain => ':',
                        YamlScalarStyle.SingleQuoted => '\'',
                        YamlScalarStyle.DoubleQuoted => '"',
                        YamlScalarStyle.Literal => '|',
                        YamlScalarStyle.Folded => '>',
                        _ => ':',
                    };
                    sb.AppendLine($"=VAL {styleChar}{EscapeEventValue(e.Value)}");
                    break;
                case YamlEventType.Alias:
                    sb.AppendLine($"=ALI *{e.Value}");
                    break;
            }
        }

        return sb.ToString();
    }

    private static string EscapeEventValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\b", "\\b")
            .Replace("\0", "\\0");
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
        string message = FixBrokenSRFormat(ex.Message);

        if (ex.Line > 0 && ex.Column > 0)
        {
            return $"Ln {ex.Line}, Col {ex.Column}: {message}";
        }

        return message;
    }
}
